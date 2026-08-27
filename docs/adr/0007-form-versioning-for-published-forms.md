# ADR 0007: Form versioning for published forms with existing submissions

## Status
Accepted

## Context
Once a form has been filled out and submitted, editing the form's structure
afterward creates a mismatch risk: a question can be deleted, an option
removed, or a type changed (e.g. checkbox to radio) after answers already
exist for it. Stored submissions would then reference questions or options
that no longer exist, or that mean something different than they did when
answered.

This is distinct from concurrent-edit conflicts (two users editing at the
same instant, handled via the optimistic concurrency token on the current
version) — this is about a form's structure drifting *after* people have
already answered it.

The fork trigger needs to be a deliberate moment, not an emergent
condition. Gating the fork on whether the current version has any
submissions yet was considered and rejected: submission existence is a
lagging signal. A form-filler could load a form and begin answering it;
before they submit, the form-builder's autosave would still see zero
submissions for that version and reconcile in place — silently
invalidating the exact page and question tree the filler is mid-way through
answering, even though the form was already live and shareable. The actual
moment a form's structure needs protecting is when it's published and made
available to fillers, not when the first answer happens to arrive. This
also matters for autosave specifically: repeated autosaves against an
unpublished, unanswered form must all target the same version rather than
each being evaluated independently against a condition that could flip
mid-session.

The existing schema has `Form` → `FormPage` → `Question` (TPH subtypes) →
`QuestionOption`, with `FormRepository.SaveAsync` reconciling pages,
questions, and options in place on every edit via `ReconcilePages` /
`ReconcileQuestions` / `ReconcileOptions`. This in-place reconciliation is
appropriate while a form has no submissions yet, but unsafe once answers
exist against it.

Two structural options were considered:

1. **Parallel version-scoped entities** — introduce `FormPageVersion`,
   `QuestionVersion`, `QuestionOptionVersion` mirroring the live tables.
   Doubles the schema surface and duplicates the discriminator/TPH mapping
   already in place for `Question`.
2. **Insert a single `FormVersion` entity between `Form` and `FormPage`** —
   repoint `FormPage.FormId` to `FormPage.FormVersionId`. `FormPage`,
   `Question` (and its subtypes), and `QuestionOption` remain unchanged.
   Versioning becomes a property of which `FormVersion` a page tree belongs
   to, not a new parallel hierarchy.

Option 2 requires no new entity types beyond `FormVersion` itself, and
reuses the existing reconciliation logic for the in-place (draft) case
without modification.

## Decision
Introduce `FormVersion` as the versioned content container, sitting between
`Form` (now a thin, stable identity) and the existing `FormPage` tree.
`FormPage.FormId` becomes `FormPage.FormVersionId`; no other entity changes.

```csharp
public class Form
{
    public required Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public ICollection<FormVersion> Versions { get; set; } = new List<FormVersion>();
}

public class FormVersion
{
    public required Guid Id { get; set; }
    public required Guid FormId { get; set; }
    public Form Form { get; set; } = null!;
    public int VersionNumber { get; set; } = 1;
    public required string Title { get; set; }
    public string? Description { get; set; }
    public int Revision { get; set; } = 1; // optimistic concurrency token, per ADR 0002
    public FormVersionStatus Status { get; set; } = FormVersionStatus.Draft;
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastModifiedAt { get; set; }
    public string? LastModifiedBy { get; set; }
    public ICollection<FormPage> Pages { get; set; } = new List<FormPage>();
}
```

`Form` carries no version pointers at all — "current" and "latest published"
are both derived from `FormVersion.VersionNumber` and `FormVersion.Status`
rather than stored:

```csharp
// current version the builder edits — the highest VersionNumber for the form,
// regardless of status (it's a Draft unless the last action was a publish
// with no edits since)
var current = await dbContext.Set<FormVersion>()
    .Where(v => v.FormId == formId)
    .OrderByDescending(v => v.VersionNumber)
    .FirstAsync(ct);

// latest published version — what the filler serves
var latestPublished = await dbContext.Set<FormVersion>()
    .Where(v => v.FormId == formId && v.Status == FormVersionStatus.Published)
    .OrderByDescending(v => v.VersionNumber)
    .FirstOrDefaultAsync(ct);
```

Older versions keep `Status == Published` after a newer draft forks past
them — `Status` records "this version was published at some point," not
"this is the currently-live one." Only `VersionNumber` ordering
distinguishes the latest published version from earlier ones. A unique
index on `(FormId, VersionNumber)` supports both queries efficiently.

This removes a category of state entirely: there's no pointer field to keep
in sync with what `Status`/`VersionNumber` already say, so a bug can't leave
`CurrentVersionId` pointing at the wrong row. The cost is that resolving
"current" or "latest published" is now a query, not a foreign-key
dereference — acceptable given `(FormId, VersionNumber)` is indexed and
these lookups happen once per request, not per row.

Fork trigger is the current version's `Status`, not submission existence:
- **Autosave or manual save, while `CurrentVersion.Status == Draft`** —
  reconcile that `FormVersion`'s page tree in place, using the existing
  `ReconcilePages` / `ReconcileQuestions` / `ReconcileOptions` logic
  unchanged, however many times it fires. `Revision` increments;
  `VersionNumber` does not.
- **Publish action** — flips the current version's `Status` to `Published`
  and stamps `PublishedAt`. No pointer field to update elsewhere; the next
  "latest published" query picks it up because it's now the
  highest-numbered `Published` version. This is the only action that locks
  a version.
- **Any edit after publish (autosave or manual)** — `SaveAsync` resolves the
  current version by `VersionNumber` and checks its `Status`. If
  `Published`, do not mutate it: create a new `Draft` `FormVersion` (next
  `VersionNumber`, fresh Guids for its entire page/question/option tree,
  built from the incoming edit) — reusing the same "insert a fresh tree"
  codepath already used for brand-new forms. Subsequent autosaves against
  that new draft go back to in-place reconciliation until it, too, is
  published.

A published form with zero submissions is still locked from further
in-place edits, by design — "published" means "someone could be looking at
it right now," not "someone has answered it."

Once a `Submission` entity is introduced, it references `FormVersionId` —
resolved via the same "latest published version" query above at the moment
the filler loads the form — so historical answers always resolve against
the exact page, question, and option rows that existed at submission time.

`GET`/`PUT /forms/{id}` continue to operate on the logical `Form.Id` and
transparently resolve to the current version via the query above; `FormDto`
gains `VersionNumber` and `Status` fields so the form-builder UI can surface
"this edit will create a new version" once the current version is
published. Publishing requires a distinct, explicit action in the UI,
separate from autosave.

Any edit to a published form forks a new version, regardless of whether the
specific change is structurally "breaking" (e.g. deleting a question) or
cosmetic (e.g. fixing a typo). A more granular breaking-vs-non-breaking
classification was considered and rejected as unnecessary complexity and
risk for this project's scope — always forking is simpler and always safe.

## Consequences
- Past `FormVersion` rows, and everything under them, are never modified
  after a fork — immutability is a natural consequence of the branch logic,
  not an explicitly enforced flag or lock.
- No new parallel entity hierarchy is needed; `FormPage`, `Question` (and
  subtypes), and `QuestionOption` are reused unchanged, just re-scoped to
  `FormVersionId`.
- Two previously conflated concerns now have distinct fields: `Revision` on
  `FormVersion` remains the optimistic-concurrency token (bumped on every
  save), while `VersionNumber` only increments on a fork. Callers checking
  for concurrent-edit conflicts and callers checking form history should not
  read the same field for both purposes.
- The fork trigger is a field read (`Status == Published` on the queried
  current version) rather than a `Submissions.Any(...)` query on every save
  — simpler and cheaper, and it removes the autosave race described in
  Context, since the trigger is now a deliberate publish action instead of
  an emergent, lagging condition.
- `Form` holds no version pointers; "current version" and "latest published
  version" are both derived by querying `FormVersion` ordered by
  `VersionNumber`, filtered by `Status` for the published case. This removes
  a category of bug (a stale or incorrectly-updated pointer) at the cost of
  a query instead of a foreign-key dereference wherever either is needed —
  acceptable given `(FormId, VersionNumber)` is indexed and each resolution
  happens once per request.
- Publishing is new, explicit user-facing surface area on the form-builder —
  a distinct action from autosave, which didn't exist under a
  submission-existence trigger.
- `FormRepository.GetBatchAsync` and other queries currently projecting
  `Title`/`Description`/`Revision` directly off `Form` need to join through
  `CurrentVersion` instead — a query change, not a conceptual one.
- Forking on every edit to an already-published version is deliberately
  coarse: a typo fix creates a full new draft just as a structural change
  would. This trades some storage and version-count growth for simplicity
  and safety, and is worth revisiting only if the project's scope grows to
  need finer-grained change classification.
- A published form with zero submissions is still locked from further
  in-place edits — a stricter, and more correct, definition of immutability
  than gating on submission existence would give.
- This is a schema change with no production data at stake (3 migrations in,
  portfolio project), so it's a clean migration rather than a data-backfill
  problem.
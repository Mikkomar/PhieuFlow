# ADR 0010: Server-side submission re-validation

Date: 2026-09-08

## Status

Accepted

## Context

ADR 0002 put answer validation in the form-filler (a UX convenience against its loaded copy)
and said the Hub would re-validate on consume, flagging a mismatch for review. ADR 0009 built
the consumer but explicitly deferred that: *"The consumer persists whatever arrives … re-validation
on consume is a later change."*

So today the client is the only gate. A stale client, a client bug, or a message published
straight onto `form-submissions` can persist a `FormSubmission` that breaks the published
form's constraints (required answer missing, number out of range, too many checkbox-group
selections) or references a `QuestionId` / `OptionId` that isn't in the form. The client-side
validator shipped first; this ADR closes the server gap it leaves open.

Two things needed deciding: what happens to an invalid message, and where the rule set lives
now that both the client and the Hub run it.

## Decision

- **Re-validate against the submitted published version.** `SubmissionMessageHandler` loads
  the full page/question tree of the exact `(FormId, FormVersionNumber)` the message names
  (`IFormRepository.GetPublishedVersionAsync`, filtered to `Status == Published`), maps it to
  `PublishedFormDto`, and validates the answers before persisting. Published versions are
  immutable (ADR 0007), so there is no constraint drift within a version — the "current vs
  loaded revision" staleness of ADR 0002 does not arise here, and a respondent who loaded an
  older version and submits after a newer one is published is still validated against the
  version they actually filled.

- **Dead-letter, do not persist.** An invalid submission returns the existing
  `SubmissionProcessingResult.Poison` — the consumer rejects it without requeue and it lands
  in `form-submissions.dlx.queue`, with one structured `LogWarning` naming the failed rules.
  No `FormSubmission` row, no `ProcessedMessage` inbox row. This revises ADR 0002's
  "flag for review": the client is the real gate, a server-invalid message means a stale or
  crafted client, and `FormSubmission` keeps having no status/flag column. Chosen over
  persist-and-mark (a migration, and every submission reader then has to reason about invalid
  rows) because the guard-rail case does not justify that surface.

- **One validator, shared.** The rule set — `IsRequired`, number/date/length/selection
  bounds (bounds only bite when a value is present), plus structural integrity (the answer
  names a question on the form, its DTO type matches that question, a chosen option belongs
  to it) — lives in `SubmissionAnswersValidator` in `PhieuFlow.Hub.Contracts`, referenced by
  both the FormFiller and the Hub. It works on `PublishedFormDto` + `IReadOnlyList<SubmissionAnswerDto>`,
  which the client already builds (`FillPage.BuildAnswers`) and the Hub can map from the
  loaded entity tree. The earlier FormFiller-local `SubmissionValidator` is removed in the
  same change.

## Consequences

- The Hub is now the authority on submission correctness, not just the client. A submitted
  form that passes the client passes the Hub by construction (same code, same inputs).
- The client and server validation can no longer drift — there is one implementation. The
  cost is a class with light logic (parsing, a type switch) living in the otherwise
  DTO-only `PhieuFlow.Hub.Contracts`.
- One extra query per consumed message: the full version tree replaces a scalar id
  projection. Fine at submission volume (prefetch 10).
- An invalid submission is lost to the DLQ with only a `LogWarning` as evidence — no DB
  record. Acceptable because the client blocks these before they are sent; a genuine
  invalid message is a stale/crafted client, observable in `form-submissions.dlx.queue` with
  an `x-death` header. A DLQ-depth signal / counter remains unbuilt (as under ADR 0009).
- The version lookup now also requires `Status == Published`, so a message naming a real but
  never-published version is `Poison` rather than persisted — an intended tightening.
- Consumer "mechanics" integration tests that deliberately submitted partial or empty answer
  sets now use an all-optional test form; the all-question-types form is reserved for the
  validation tests.

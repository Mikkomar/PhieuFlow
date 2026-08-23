# ADR 0002: Submission validation ownership and staleness handling

## Status
Accepted

## Context
Validation constraints (required, min/max length, min/max selections) are
not a separate schema artifact — they are properties on the question
definitions that ship with the form when the form-filler loads it. The
form-filler validates answers against this loaded copy before submitting.

Because the form-filler holds a copy of the form taken at load time, a gap
exists: the form owner could edit constraints in the builder (e.g. make an
optional question required, tighten a max-length) between when the filler
loaded the form and when the user submits. Validating only against the
filler's stale copy risks accepting submissions the current form definition
would reject.

Two ways to close this gap:
1. Form-filler re-fetches the current form immediately before submit and
   validates against that — guarantees freshness, costs a round-trip.
2. Form-filler validates against its loaded copy; the hub re-validates
   against its current copy on consume, and a defined behavior handles
   mismatches (reject, dead-letter, flag for review).

## Decision
Form-filler validates against its loaded copy (fast, no extra round-trip on
every keystroke or submit). The hub re-validates against the current form
definition on message consume, using a revision/last-modified marker on the
form entity.

The submission message payload includes the form's revision identifier at
the time of load. On consume, if the hub's current revision doesn't match:
log a warning and flag the submission for review rather than silently
accepting or silently discarding it. Full reconciliation (e.g. re-prompting
the user, partial re-validation) is out of scope for this project.

## Consequences
- The hub remains the authority on form correctness; the filler's validation
  is a UX convenience, not the system's source of truth.
- Requires the form entity to carry a revision/timestamp that increments on
  edit — a natural fit alongside the outbox/inbox pattern already in use.
- The mismatch-handling path (flag for review, no auto-reconciliation) is a
  deliberately minimal implementation. Worth stating plainly in the repo
  rather than leaving it looking like an oversight.

# ADR 0001: Synchronous form management, asynchronous submission

## Status
Accepted

## Context
The system has three services: a form-builder UI, a form-filler UI, and a hub
that owns the database. Two interaction types need a communication mechanism:

1. Building a form — add/edit/delete/reorder questions, set validation rules
   (required, min/max length, min/max selections).
2. Submitting a filled-out form.

The initial design routed all inter-service communication through RabbitMQ,
including form building. Form building is request/response CRUD: the user
clicks save and needs an immediate success/failure result. Routing that
through a broker means building request/reply semantics on top of messaging
(correlation IDs, reply queues, timeouts) or accepting a fire-and-poll UX —
complexity with no corresponding benefit, and it dilutes the RabbitMQ
patterns (nack/DLX, x-death tracking, quorum delivery limits) the project
exists to demonstrate, since those patterns exist to handle failure in
genuinely async flows, not synchronous edits.

Form submission is a genuine event: it happens once, doesn't need an
immediate synchronous result beyond "received," and benefits from retry,
dead-lettering, and idempotent processing if the hub is briefly unavailable.

## Decision
- **Form-builder ↔ Hub**: synchronous REST API. All CRUD on forms and
  questions goes through direct HTTP calls.
- **Form-filler ↔ Hub reads**: synchronous REST API (fetching a form to
  display).
- **Form-filler → Hub, submission only**: asynchronous via RabbitMQ. This is
  the single deliberate async boundary in the system.

## Consequences
- RabbitMQ patterns (inbox table for idempotency, outbox pattern for
  publish-with-persistence, DLX/nack, quorum queue delivery limits) apply
  cleanly to one well-defined flow instead of being spread thin across
  unrelated CRUD operations.
- Two communication mechanisms exist in the system instead of one uniform
  approach. This is intentional — the split itself is the point being
  demonstrated, not an inconsistency to clean up.
- AsyncAPI documentation only needs to describe the submission event, giving
  it a narrow, well-defined scope rather than trying to model CRUD operations
  as async messages.

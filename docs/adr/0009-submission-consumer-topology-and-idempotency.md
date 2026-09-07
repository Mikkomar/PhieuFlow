# ADR 0009: Submission consumer topology and idempotency

## Status
Accepted

## Context
ADR 0001 fixed form submission as the one asynchronous boundary; ADR 0008 built the
publisher half — the form-filler puts a `FormSubmissionRequest` onto the durable quorum
queue `form-submissions` — and pinned the queue name and message framing, but left the
consumer, the inbox, and the dead-letter/nack wiring unbuilt. Messages accumulate with
nothing draining them, so no `FormSubmission` row is ever written outside the test seed.

Standing up the consumer needs three things decided: how the consumer host is shaped and
how it acks, the consume-side queue topology (dead-letter path, bounded redelivery), and
how to add the new queue arguments without a rejected redeclare of the ADR 0008 queue.

## Decision

- **Consumer host.** A `BackgroundService` in the Hub (`SubmissionConsumerService`) owns
  one `IChannel` off the Aspire singleton `IConnection`. It consumes with manual ack and a
  prefetch limit (`SubmissionConsumerOptions.PrefetchCount`, default 10). Each delivery
  opens a DI scope and calls `SubmissionMessageHandler`, which holds no RabbitMQ types so it
  can be tested against a real database with no broker. The handler runs its reads plus one
  `SaveChangesAsync` through the EF retry execution strategy, the same pattern as
  `MigrationService.Worker`.

- **Topology, declaration-only.** There is no shared broker admin, so nothing relies on a
  broker policy — every argument is an `x-argument` set at declare time. The consumer
  declares a direct dead-letter exchange `form-submissions.dlx` and a durable quorum queue
  `form-submissions.dlx.queue` bound to it, then declares the main queue. The main queue
  gains `x-delivery-limit = 5`, `x-dead-letter-exchange`, and `x-dead-letter-routing-key`.

- **Shared declaration, changed in place.** The full argument set moves into
  `SubmissionQueue.MainQueueArguments()` in `PhieuFlow.Hub.Contracts` (a plain dictionary,
  no RabbitMQ dependency in that assembly) and the form-filler publisher switches to it in
  the same change, so both sides declare the main queue identically whichever connects
  first. The queue keeps its name `form-submissions`: RabbitMQ rejects a redeclare of a
  live queue with changed arguments, but no environment is running the ADR 0008 version, so
  the argument set is simply changed in place rather than versioning the queue name.

- **Idempotency.** An inbox table `ProcessedMessages` keyed on the publisher's per-publish
  `MessageId` (its primary key). The `FormSubmission`, its answers, and the inbox row are
  written in one `SaveChangesAsync` — one transaction — so either all land or none do. A
  unique-key violation on the inbox row means a concurrent delivery of the same message
  won the race; that is treated as a duplicate, not an error.

- **Transient vs poison.** A malformed body (no usable `MessageId`, invalid JSON) or an
  unknown `(FormId, FormVersionNumber)` or an unmappable answer is rejected without requeue
  and dead-letters immediately. A transient broker/database fault — after the EF retry
  strategy has exhausted its own retries — is nacked with requeue; `x-delivery-limit` caps
  the redelivery and an exhausted message dead-letters automatically. A redelivery whose
  `MessageId` is already in the inbox is acked as a no-op.

- **Staleness stays deferred (ADR 0002).** The consumer persists whatever arrives. No
  revision marker is added to `FormSubmissionRequest` and no status/flag field to
  `FormSubmission`; re-validation on consume is a later change.

## Consequences
- The consumer half of ADR 0001 now exists; the Hub takes `.WithReference(rabbitmq)` in the
  AppHost. A submitted form is persisted end to end.
- The E2E submission specs stay skipped — they assert Hub state through
  `GET /forms/{id}/submissions`, which is still unbuilt, as is the filler-side outbox that
  would close the crash-between-persist-and-publish gap.
- A genuinely poisonous message is observable in `form-submissions.dlx.queue` via the
  management plugin, with an `x-death` header saying whether it was rejected or hit the
  delivery limit.
- `x-delivery-limit` requires a quorum queue, which `form-submissions` already is.
- Shutdown relies on the broker requeueing unacked in-flight deliveries on channel close;
  the inbox makes that safe. An explicit in-flight drain is a deferred nicety.

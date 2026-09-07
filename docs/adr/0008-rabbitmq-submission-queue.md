# ADR 0008: RabbitMQ transport and the submission queue topology

## Status
Accepted

## Context
ADR 0001 fixed form submission as the one asynchronous boundary in the
system: the form-filler publishes a completed response and a Hub consumer
persists it, with retry and dead-lettering when the Hub is briefly
unavailable. Until now the form-filler side was a stub
(`LoggingSubmissionPublisher`) that wrote the response to the log and
reported success, so the fill flow worked end to end on the client only.

Standing up the real transport needs three things pinned down: how the
broker is orchestrated locally, what the queue looks like, and how a
message is framed on the wire so the not-yet-built consumer can read it.

## Decision
- **Broker orchestration.** The AppHost adds a `rabbitmq` resource
  (`Aspire.Hosting.RabbitMQ`) with a data volume and the management plugin.
  The data volume keeps persistent messages across a broker restart; the
  management plugin exposes queue and dead-letter state for inspection
  during development. The form-filler takes a `WithReference` on it; the Hub
  takes one when its consumer half is built.
- **Queue.** One queue, `form-submissions`, its name a shared constant
  (`PhieuFlow.Hub.Contracts.SubmissionQueue.Name`) so the future consumer
  declares the identical queue. It is a **durable quorum queue**
  (`x-queue-type: quorum`) — quorum is the queue type the delivery-limit and
  nack patterns ADR 0001 exists to demonstrate are defined on. Whichever
  side connects first declares it; the declaration is idempotent.
- **Message framing.** The body is the existing
  `FormSubmissionRequest` contract serialised as UTF-8 JSON by
  `System.Text.Json` (the polymorphic answer discriminator is already on the
  DTO). Messages are published **persistent** with `application/json`
  content type and a fresh `MessageId` per publish — the id is the key the
  consumer's inbox table will dedupe redeliveries on.
- The client-side stub and its interface (`ISubmissionPublisher` →
  `RabbitMqSubmissionPublisher`) keep the same shape, so nothing in the page
  layer changes.

## Consequences
- The publisher half of ADR 0001 now exists. The Hub consumer, the inbox
  table for idempotency, the outbox for publish-with-persistence, and the
  DLX/nack wiring are still to build; the queue and message contract they
  depend on are now fixed.
- A submission survives a broker restart (durable queue + persistent
  message + data volume) but is still lost if the form-filler crashes
  between persisting nothing and publishing — the outbox pattern closes that
  gap and is deferred with the rest of the consumer work.
- Messages accumulate in `form-submissions` with no consumer draining them
  until the Hub side lands. This is intended: it lets the publish path be
  exercised and inspected on its own.
- The E2E submission specs (ADR 0006) stay skipped — they assert Hub state
  after the round-trip and need the consumer plus a `GET /forms/{id}/submissions`
  endpoint.

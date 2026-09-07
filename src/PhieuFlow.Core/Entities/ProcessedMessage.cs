namespace PhieuFlow.Core.Entities;

/// <summary>
/// Inbox row for submission-consumer idempotency (ADR 0001/0009). Keyed on the publisher's
/// per-publish <c>MessageId</c>; a redelivery whose id is already here is acked as a no-op.
/// The row is written in the same transaction as the <see cref="FormSubmission"/> it
/// records, so either both land or neither does.
/// </summary>
public class ProcessedMessage
{
    public required Guid MessageId { get; set; }
    public DateTimeOffset ProcessedAt { get; set; }
}

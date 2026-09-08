namespace PhieuFlow.Core.Entities;

/// <summary>
/// Inbox row that makes the submission consumer idempotent. The key is the publisher's
/// <c>MessageId</c>. A redelivery of a known id is acknowledged and ignored. This row is
/// written in the same transaction as its <see cref="FormSubmission"/>.
/// </summary>
public class ProcessedMessage
{
    public required Guid MessageId { get; set; }
    public DateTimeOffset ProcessedAt { get; set; }
}

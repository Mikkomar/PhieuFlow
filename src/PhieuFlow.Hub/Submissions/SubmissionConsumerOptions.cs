namespace PhieuFlow.Hub.Submissions;

/// <summary>
/// Tuning for <see cref="SubmissionConsumerService"/>. Bind from the
/// <c>SubmissionConsumer</c> configuration section. The delivery limit is not here — the
/// publisher needs the same value, so it lives on
/// <see cref="Contracts.Submissions.SubmissionQueue.DeliveryLimit"/>.
/// </summary>
public sealed record SubmissionConsumerOptions
{
    public const string SectionName = "SubmissionConsumer";

    /// <summary>
    /// <c>BasicQos</c> prefetch — unacked deliveries the broker will hand this consumer at
    /// once. Higher trades strict ordering for throughput; the <c>ProcessedMessages</c>
    /// primary key still makes a concurrent duplicate a no-op.
    /// </summary>
    public ushort PrefetchCount { get; init; } = 10;
}

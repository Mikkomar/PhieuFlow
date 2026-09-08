namespace PhieuFlow.Hub.Submissions;

/// <summary>
/// Tuning for <see cref="SubmissionConsumerService"/>, bound from the <c>SubmissionConsumer</c>
/// section. The delivery limit lives on <see cref="Contracts.Submissions.SubmissionQueue"/>
/// instead, because the publisher needs the same value.
/// </summary>
public sealed record SubmissionConsumerOptions
{
    public const string SectionName = "SubmissionConsumer";

    /// <summary>
    /// <c>BasicQos</c> prefetch: unacked deliveries handed to this consumer at once. Higher
    /// trades ordering for throughput. The <c>ProcessedMessages</c> key keeps duplicates safe.
    /// </summary>
    public ushort PrefetchCount { get; init; } = 10;
}

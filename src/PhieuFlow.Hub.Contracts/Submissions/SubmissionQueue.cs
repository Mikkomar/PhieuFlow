namespace PhieuFlow.Hub.Contracts.Submissions;

/// <summary>
/// The single async boundary in the system (ADR 0001/0009). The form-filler publishes a
/// <see cref="FormSubmissionRequest"/> here and the Hub consumer drains it. It is declared
/// as a durable quorum queue so a submission survives a broker restart; a bounded
/// <c>x-delivery-limit</c> caps redelivery and an exhausted message dead-letters onto
/// <see cref="DeadLetterQueueName"/>, as do messages the consumer rejects as poison.
///
/// The publisher and the consumer both declare the main queue with
/// <see cref="MainQueueArguments"/>, so the declaration is identical whichever side
/// connects first. The consumer additionally declares the dead-letter exchange and queue.
/// </summary>
public static class SubmissionQueue
{
    public const string Name = "form-submissions";
    public const string DeadLetterExchangeName = "form-submissions.dlx";
    public const string DeadLetterQueueName = "form-submissions.dlx.queue";
    public const string DeadLetterRoutingKey = "dead";

    /// <summary>
    /// Redeliveries a quorum message gets before the broker dead-letters it
    /// (<c>x-delivery-limit</c>). Shared so the publisher's declare matches the consumer's.
    /// </summary>
    public const int DeliveryLimit = 5;

    /// <summary>
    /// Arguments both sides pass to <c>QueueDeclareAsync</c> for <see cref="Name"/>. A plain
    /// dictionary so this assembly takes no dependency on the RabbitMQ client.
    /// </summary>
    public static Dictionary<string, object?> MainQueueArguments() => new()
    {
        ["x-queue-type"] = "quorum",
        ["x-delivery-limit"] = DeliveryLimit,
        ["x-dead-letter-exchange"] = DeadLetterExchangeName,
        ["x-dead-letter-routing-key"] = DeadLetterRoutingKey,
    };
}

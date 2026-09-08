namespace PhieuFlow.Hub.Contracts.Submissions;

/// <summary>
/// The system's one async boundary: the form-filler publishes a
/// <see cref="FormSubmissionRequest"/> here, the Hub consumer reads it. A durable quorum
/// queue. <c>x-delivery-limit</c> routes exhausted or poison messages to the dead-letter queue.
/// </summary>
public static class SubmissionQueue
{
    public const string Name = "form-submissions";
    public const string DeadLetterExchangeName = "form-submissions.dlx";
    public const string DeadLetterQueueName = "form-submissions.dlx.queue";
    public const string DeadLetterRoutingKey = "dead";

    /// <summary>
    /// Redeliveries before the broker dead-letters a message (<c>x-delivery-limit</c>).
    /// Shared so the publisher and consumer declare the queue the same way.
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

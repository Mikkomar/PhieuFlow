using System.Text.Json;
using PhieuFlow.Hub.Contracts.Submissions;
using RabbitMQ.Client;

namespace PhieuFlow.FormFiller.Submissions;

/// <summary>
/// Publishes a completed response onto the durable <see cref="SubmissionQueue"/> quorum
/// queue (ADR 0001). The message is persistent and carries a fresh <c>MessageId</c> so the
/// Hub consumer's inbox table can drop a redelivery. Nothing in the page layer changes
/// when the consumer half lands.
/// </summary>
public sealed class RabbitMqSubmissionPublisher(
    IConnection connection,
    ILogger<RabbitMqSubmissionPublisher> logger) : ISubmissionPublisher
{
    // A quorum queue must be declared durable; the delivery-limit / nack patterns
    // (ADR 0001) are defined on this queue type.
    private static readonly Dictionary<string, object?> QuorumQueueArguments = new()
    {
        ["x-queue-type"] = "quorum",
    };

    public async Task PublishAsync(FormSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        // Idempotent: the consumer declares the same queue, whichever side races first.
        await channel.QueueDeclareAsync(
            queue: SubmissionQueue.Name,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: QuorumQueueArguments,
            cancellationToken: cancellationToken);

        var body = JsonSerializer.SerializeToUtf8Bytes(request);
        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = Guid.NewGuid().ToString(),
        };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: SubmissionQueue.Name,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "Submission for form {FormId} v{VersionNumber} ({AnswerCount} answer(s)) published to {Queue} as {MessageId}.",
            request.FormId,
            request.FormVersionNumber,
            request.Answers.Count,
            SubmissionQueue.Name,
            properties.MessageId);
    }
}

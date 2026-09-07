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
    public async Task PublishAsync(FormSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = Guid.NewGuid().ToString(),
        };

        try
        {
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

            // Idempotent: the consumer declares the same queue with the same arguments,
            // whichever side races first. The consumer also declares the dead-letter
            // exchange/queue that MainQueueArguments points at.
            await channel.QueueDeclareAsync(
                queue: SubmissionQueue.Name,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: SubmissionQueue.MainQueueArguments(),
                cancellationToken: cancellationToken);

            var body = JsonSerializer.SerializeToUtf8Bytes(request);

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: SubmissionQueue.Name,
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Broker down, channel/connection fault, queue-argument mismatch, or a
            // serialization failure — the submission is lost unless the caller retries.
            logger.LogError(
                ex,
                "Publishing submission for form {FormId} v{VersionNumber} to {Queue} failed.",
                request.FormId,
                request.FormVersionNumber,
                SubmissionQueue.Name);
            throw;
        }

        logger.LogInformation(
            "Submission for form {FormId} v{VersionNumber} ({AnswerCount} answer(s)) published to {Queue} as {MessageId}.",
            request.FormId,
            request.FormVersionNumber,
            request.Answers.Count,
            SubmissionQueue.Name,
            properties.MessageId);
    }
}

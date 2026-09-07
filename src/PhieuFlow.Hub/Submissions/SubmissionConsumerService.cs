using System.Text.Json;
using Microsoft.Extensions.Options;
using PhieuFlow.Hub.Contracts.Submissions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PhieuFlow.Hub.Submissions;

/// <summary>
/// Drains the <see cref="SubmissionQueue"/> and persists each completed response via
/// <see cref="SubmissionMessageHandler"/> (ADR 0001/0009). Owns one channel off the Aspire
/// singleton <see cref="IConnection"/>, declares the full topology (main queue + dead-letter
/// exchange/queue), and consumes with manual ack and a prefetch limit.
///
/// Ack policy: a persisted or already-seen message is acked; a poison message (bad body,
/// unknown form/version, unmappable answer) is rejected without requeue and dead-letters; a
/// transient fault is nacked with requeue and bounded by the queue's <c>x-delivery-limit</c>.
/// </summary>
public sealed class SubmissionConsumerService(
    IConnection connection,
    IServiceScopeFactory scopeFactory,
    IOptions<SubmissionConsumerOptions> options,
    ILogger<SubmissionConsumerService> logger) : BackgroundService
{
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        // Dead-letter path. Declaration-only — there is no shared broker admin, so nothing
        // relies on a policy. The publisher declares only the main queue; the consumer owns
        // the exchange/queue its x-dead-letter-exchange argument points at.
        await _channel.ExchangeDeclareAsync(
            exchange: SubmissionQueue.DeadLetterExchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await _channel.QueueDeclareAsync(
            queue: SubmissionQueue.DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-queue-type"] = "quorum" },
            cancellationToken: stoppingToken);

        await _channel.QueueBindAsync(
            queue: SubmissionQueue.DeadLetterQueueName,
            exchange: SubmissionQueue.DeadLetterExchangeName,
            routingKey: SubmissionQueue.DeadLetterRoutingKey,
            cancellationToken: stoppingToken);

        // Same arguments as RabbitMqSubmissionPublisher — an identical redeclare, whichever
        // side connected first.
        await _channel.QueueDeclareAsync(
            queue: SubmissionQueue.Name,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: SubmissionQueue.MainQueueArguments(),
            cancellationToken: stoppingToken);

        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: options.Value.PrefetchCount,
            global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += OnMessageAsync;

        await _channel.BasicConsumeAsync(
            queue: SubmissionQueue.Name,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        logger.LogInformation(
            "Submission consumer listening on {Queue} (prefetch {Prefetch}).",
            SubmissionQueue.Name, options.Value.PrefetchCount);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Host shutdown.
        }
    }

    private async Task OnMessageAsync(object sender, BasicDeliverEventArgs eventArgs)
    {
        var channel = _channel!;

        if (!Guid.TryParse(eventArgs.BasicProperties.MessageId, out var messageId))
        {
            logger.LogWarning(
                "Submission delivery {DeliveryTag} has no usable MessageId; dead-lettering.",
                eventArgs.DeliveryTag);
            await channel.BasicRejectAsync(eventArgs.DeliveryTag, requeue: false);
            return;
        }

        FormSubmissionRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<FormSubmissionRequest>(eventArgs.Body.Span);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Submission message {MessageId} is not valid JSON; dead-lettering.", messageId);
            await channel.BasicRejectAsync(eventArgs.DeliveryTag, requeue: false);
            return;
        }

        if (request is null)
        {
            logger.LogWarning("Submission message {MessageId} deserialized to null; dead-lettering.", messageId);
            await channel.BasicRejectAsync(eventArgs.DeliveryTag, requeue: false);
            return;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<SubmissionMessageHandler>();
            var result = await handler.HandleAsync(request, messageId, CancellationToken.None);

            switch (result)
            {
                case SubmissionProcessingResult.Persisted:
                case SubmissionProcessingResult.DuplicateIgnored:
                    await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);
                    break;

                case SubmissionProcessingResult.Poison:
                    await channel.BasicRejectAsync(eventArgs.DeliveryTag, requeue: false);
                    break;
            }
        }
        catch (Exception ex)
        {
            // Transient: broker/DB fault after the EF retry strategy exhausted its own
            // retries. Requeue; x-delivery-limit bounds it and dead-letters an exhausted
            // message. The inbox makes a later redelivery safe.
            logger.LogError(ex, "Transient failure processing submission {MessageId}; requeueing.", messageId);
            await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: true);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        if (_channel is not null)
        {
            // Unacked in-flight deliveries are requeued by the broker on close; the
            // ProcessedMessages inbox dedups any that had already committed.
            await _channel.CloseAsync(cancellationToken);
            await _channel.DisposeAsync();
            _channel = null;
        }
    }
}

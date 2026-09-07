using PhieuFlow.Hub.Contracts.Submissions;

namespace PhieuFlow.FormFiller.Submissions;

/// <summary>
/// Hands a completed <see cref="FormSubmissionRequest"/> to the submission transport: async
/// RabbitMQ (ADR 0001), implemented by <see cref="RabbitMqSubmissionPublisher"/>. The Hub
/// consumer that drains the queue is not built yet.
/// </summary>
public interface ISubmissionPublisher
{
    Task PublishAsync(FormSubmissionRequest request, CancellationToken cancellationToken = default);
}

using PhieuFlow.Hub.Contracts.Submissions;

namespace PhieuFlow.FormFiller.Submissions;

/// <summary>
/// Hands a completed <see cref="FormSubmissionRequest"/> to the submission transport.
/// Implemented by <see cref="RabbitMqSubmissionPublisher"/>.
/// </summary>
public interface ISubmissionPublisher
{
    Task PublishAsync(FormSubmissionRequest request, CancellationToken cancellationToken = default);
}

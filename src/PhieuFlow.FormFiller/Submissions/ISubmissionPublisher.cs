using PhieuFlow.Hub.Contracts.Submissions;

namespace PhieuFlow.FormFiller.Submissions;

/// <summary>
/// Hands a completed <see cref="FormSubmissionRequest"/> to the submission transport. The
/// real transport is async RabbitMQ (ADR 0001); until that boundary exists,
/// <see cref="LoggingSubmissionPublisher"/> stands in.
/// </summary>
public interface ISubmissionPublisher
{
    Task PublishAsync(FormSubmissionRequest request, CancellationToken cancellationToken = default);
}

using System.Text.Json;
using PhieuFlow.Hub.Contracts.Submissions;

namespace PhieuFlow.FormFiller.Submissions;

/// <summary>
/// Interim <see cref="ISubmissionPublisher"/> that records the submission to the log and
/// reports success. Replaced by the RabbitMQ publisher (ADR 0001) once that boundary is
/// built; nothing in the page layer changes when it is.
/// </summary>
public sealed class LoggingSubmissionPublisher(ILogger<LoggingSubmissionPublisher> logger)
    : ISubmissionPublisher
{
    public Task PublishAsync(FormSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Form submission received: form {FormId} v{VersionNumber}, {AnswerCount} answer(s). Payload: {Payload}",
            request.FormId,
            request.FormVersionNumber,
            request.Answers.Count,
            JsonSerializer.Serialize(request));

        return Task.CompletedTask;
    }
}

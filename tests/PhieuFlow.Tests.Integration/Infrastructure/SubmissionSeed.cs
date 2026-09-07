using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PhieuFlow.Core.Entities;
using PhieuFlow.Persistence;

namespace PhieuFlow.Tests.Integration.Infrastructure;

/// <summary>
/// Writes a <see cref="FormSubmission"/> row straight through <see cref="HubDbContext"/>. The
/// real ingestion path is now the RabbitMQ consumer (<c>SubmissionMessageHandler</c>, ADR
/// 0009); this stays as a fast direct-write shortcut for the tests that only need a form to
/// have responses — the "form has responses" guard on delete and its projection onto the
/// forms list — and do not care how the row got there.
/// </summary>
internal static class SubmissionSeed
{
    public static async Task AddAsync(IServiceProvider services, Guid formId)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDbContext>();

        var versionId = await db.FormVersions
            .Where(v => v.FormId == formId)
            .Select(v => v.Id)
            .FirstAsync();

        var submissionId = Guid.NewGuid();
        db.FormSubmissions.Add(new FormSubmission
        {
            Id = submissionId,
            FormId = formId,
            FormVersionId = versionId,
            FormVersionNumber = 1,
            SubmittedAt = DateTimeOffset.UtcNow,
            Answers =
            {
                new ValueSubmissionAnswer
                {
                    Id = Guid.NewGuid(), FormSubmissionId = submissionId,
                    QuestionId = Guid.NewGuid(), QuestionText = "Answer me", Order = 0, Value = "a response",
                },
                new OptionSubmissionAnswer
                {
                    Id = Guid.NewGuid(), FormSubmissionId = submissionId,
                    QuestionId = Guid.NewGuid(), QuestionText = "Pick one", Order = 1, OptionId = Guid.NewGuid(),
                },
            },
        });
        await db.SaveChangesAsync();
    }
}

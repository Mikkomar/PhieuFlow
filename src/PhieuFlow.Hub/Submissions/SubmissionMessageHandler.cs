using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PhieuFlow.Core.Entities;
using PhieuFlow.Hub.Contracts.Submissions;
using PhieuFlow.Hub.Mapping;
using PhieuFlow.Persistence;

namespace PhieuFlow.Hub.Submissions;

/// <summary>
/// Persists one completed response (ADR 0001/0009). Transport-independent — it takes a
/// deserialized <see cref="FormSubmissionRequest"/> and the publisher's message id, and
/// writes the <see cref="FormSubmission"/>, its answers, and the inbox row in a single
/// transaction. Kept free of RabbitMQ types so it can be exercised against a real database
/// with no broker.
/// </summary>
public sealed class SubmissionMessageHandler(
    HubDbContext db,
    ILogger<SubmissionMessageHandler> logger)
{
    public async Task<SubmissionProcessingResult> HandleAsync(
        FormSubmissionRequest request,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        // AddSqlServerDbContext turns on EnableRetryOnFailure, so the read(s) + the single
        // SaveChanges run as one unit through the execution strategy (same pattern as
        // MigrationService.Worker). ChangeTracker.Clear keeps a retried attempt from
        // re-adding the graph from the previous try.
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();

            if (await db.ProcessedMessages.AnyAsync(m => m.MessageId == messageId, cancellationToken))
            {
                logger.LogInformation("Submission message {MessageId} already processed; ignoring.", messageId);
                return SubmissionProcessingResult.DuplicateIgnored;
            }

            // Unique index FormVersions(FormId, VersionNumber). A miss means the message
            // names a form/version the Hub has never had — it can never succeed.
            var formVersionId = await db.FormVersions
                .Where(v => v.FormId == request.FormId && v.VersionNumber == request.FormVersionNumber)
                .Select(v => (Guid?)v.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (formVersionId is null)
            {
                logger.LogWarning(
                    "Submission message {MessageId} names unknown form {FormId} v{VersionNumber}; dead-lettering.",
                    messageId, request.FormId, request.FormVersionNumber);
                return SubmissionProcessingResult.Poison;
            }

            var submissionId = Guid.NewGuid();

            List<SubmissionAnswer> answers;
            try
            {
                answers = request.Answers
                    .Select(a => SubmissionRequestMapper.ToEntity(a, submissionId))
                    .ToList();
            }
            catch (NotSupportedException ex)
            {
                logger.LogWarning(
                    ex, "Submission message {MessageId} has an unmappable answer; dead-lettering.", messageId);
                return SubmissionProcessingResult.Poison;
            }

            db.FormSubmissions.Add(new FormSubmission
            {
                Id = submissionId,
                FormId = request.FormId,
                FormVersionId = formVersionId.Value,
                FormVersionNumber = request.FormVersionNumber,
                SubmittedAt = DateTimeOffset.UtcNow,
                Answers = answers,
            });
            db.ProcessedMessages.Add(new ProcessedMessage
            {
                MessageId = messageId,
                ProcessedAt = DateTimeOffset.UtcNow,
            });

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // A concurrent delivery of the same message id won the race on the inbox
                // primary key. That delivery persisted the submission; this one is a no-op.
                logger.LogInformation(
                    "Submission message {MessageId} was persisted concurrently; ignoring.", messageId);
                return SubmissionProcessingResult.DuplicateIgnored;
            }

            logger.LogInformation(
                "Persisted submission {SubmissionId} for form {FormId} v{VersionNumber} ({AnswerCount} answer(s)) from message {MessageId}.",
                submissionId, request.FormId, request.FormVersionNumber, answers.Count, messageId);

            return SubmissionProcessingResult.Persisted;
        });
    }

    // SQL Server 2601 (unique index) / 2627 (unique constraint).
    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}

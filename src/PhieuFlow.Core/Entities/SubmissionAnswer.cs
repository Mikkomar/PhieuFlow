namespace PhieuFlow.Core.Entities;

public abstract class SubmissionAnswer
{
    public required Guid Id { get; set; }

    public required Guid FormSubmissionId { get; set; }
    public FormSubmission FormSubmission { get; set; } = null!;

    public Guid QuestionId { get; set; }              // a reference value, not a foreign key by design
    public required string QuestionText { get; set; } // a copy of the question text taken at submit time
    public int Order { get; set; }
}

namespace PhieuFlow.Core.Entities;

public abstract class SubmissionAnswer
{
    public required Guid Id { get; set; }

    public required Guid FormSubmissionId { get; set; }
    public FormSubmission FormSubmission { get; set; } = null!;

    public Guid QuestionId { get; set; }              // reference — deliberately NOT an FK
    public required string QuestionText { get; set; } // snapshot
    public int Order { get; set; }
}

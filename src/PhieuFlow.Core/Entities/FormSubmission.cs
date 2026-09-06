namespace PhieuFlow.Core.Entities;

public class FormSubmission
{
    public required Guid Id { get; set; }

    public required Guid FormId { get; set; }
    public Form Form { get; set; } = null!;

    public required Guid FormVersionId { get; set; }
    public FormVersion FormVersion { get; set; } = null!;

    public int FormVersionNumber { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }

    public ICollection<SubmissionAnswer> Answers { get; set; } = new List<SubmissionAnswer>();
}

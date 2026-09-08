namespace PhieuFlow.Persistence.Projections;

/// <summary>
/// A read model of one <c>FormSubmission</c> for the FormBuilder Responses view. Each
/// answer is a display string: option ids resolved to labels, a checkbox group's
/// selections joined by commas.
/// </summary>
public class SubmissionListItem
{
    public required Guid Id { get; set; }
    public required DateTimeOffset SubmittedAt { get; set; }
    public required int FormVersionNumber { get; set; }
    public required IReadOnlyList<SubmissionAnswerItem> Answers { get; set; }
}

public class SubmissionAnswerItem
{
    public required Guid QuestionId { get; set; }
    public required string QuestionText { get; set; }
    public int Order { get; set; }
    public string? Value { get; set; }
}

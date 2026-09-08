namespace PhieuFlow.Hub.Contracts.Submissions;

/// <summary>
/// One keyset page of a form's persisted submissions, ordered by ascending
/// <see cref="SubmissionListItemDto.Id"/>. The client sorts newest-first for display.
/// </summary>
public class SubmissionBatchResponse
{
    public required IReadOnlyList<SubmissionListItemDto> Items { get; set; }
    public Guid? NextStartId { get; set; }
}

/// <summary>
/// A persisted response, flattened for display. Each answer's
/// <see cref="SubmissionAnswerValueDto.Value"/> is a ready-to-render string: option ids
/// resolved to labels, a checkbox group's selections joined by commas.
/// </summary>
public class SubmissionListItemDto
{
    public required Guid Id { get; set; }
    public required DateTimeOffset SubmittedAt { get; set; }
    public required int FormVersionNumber { get; set; }
    public required IReadOnlyList<SubmissionAnswerValueDto> Answers { get; set; }
}

/// <summary>One question's answer on a submission, already reduced to a display string.</summary>
public class SubmissionAnswerValueDto
{
    public required Guid QuestionId { get; set; }
    public required string QuestionText { get; set; }
    public int Order { get; set; }

    /// <summary>
    /// The raw text for a value question, <c>Yes</c>/<c>No</c> for a checkbox, or the option
    /// label(s) for a choice question. <c>null</c> when a value question was left blank.
    /// </summary>
    public string? Value { get; set; }
}

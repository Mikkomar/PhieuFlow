namespace PhieuFlow.Hub.Contracts.Submissions;

/// <summary>
/// One page of a form's persisted submissions, keyset-paged by
/// <see cref="SubmissionListItemDto.Id"/> exactly like <c>FormBatchResponse</c>. The keyset
/// is a plain ascending id; newest-first ordering for display is the client's job (the
/// FormBuilder session sorts the accumulated rows).
/// </summary>
public class SubmissionBatchResponse
{
    public required IReadOnlyList<SubmissionListItemDto> Items { get; set; }
    public Guid? NextStartId { get; set; }
}

/// <summary>
/// A persisted response, flattened for display. Every answer's
/// <see cref="SubmissionAnswerValueDto.Value"/> is already a string the builder can render —
/// option ids are resolved to their labels server-side against the (immutable, ADR 0007)
/// published version the response was filled against, and a checkbox group's per-selection
/// rows are collapsed into one comma-joined entry.
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
    /// The raw text for a value question, <c>"Yes"</c>/<c>"No"</c> for a checkbox, the option
    /// label (or comma-joined labels for a multi-select) for a choice question. <c>null</c>
    /// when the respondent left a value question blank.
    /// </summary>
    public string? Value { get; set; }
}

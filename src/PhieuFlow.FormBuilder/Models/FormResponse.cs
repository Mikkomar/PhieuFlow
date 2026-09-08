namespace PhieuFlow.FormBuilder.Models;

/// <summary>One persisted submission as the Responses view renders it.</summary>
public sealed class FormResponse
{
    public required Guid Id { get; init; }
    public required DateTimeOffset SubmittedAt { get; init; }
    public required int FormVersionNumber { get; init; }
    public required IReadOnlyList<FormResponseAnswer> Answers { get; init; }
}

public sealed class FormResponseAnswer
{
    public required Guid QuestionId { get; init; }
    public required string QuestionText { get; init; }
    public int Order { get; init; }

    /// <summary>Display-ready value; <c>null</c> when the respondent left a value question blank.</summary>
    public string? Value { get; init; }
}

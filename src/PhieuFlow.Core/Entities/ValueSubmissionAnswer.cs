namespace PhieuFlow.Core.Entities;

/// <summary>An answer to a TextArea, Number, or Calendar question. Holds the raw input string.</summary>
public class ValueSubmissionAnswer : SubmissionAnswer
{
    public string? Value { get; set; }
}

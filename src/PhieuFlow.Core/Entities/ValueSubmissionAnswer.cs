namespace PhieuFlow.Core.Entities;

/// <summary>
/// An answer to a question whose input accepts a non-predetermined value —
/// TextArea, Number, Calendar. Holds the raw input string.
/// </summary>
public class ValueSubmissionAnswer : SubmissionAnswer
{
    public string? Value { get; set; }
}

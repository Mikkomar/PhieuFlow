namespace PhieuFlow.Core.Entities;

/// <summary>An answer to a single yes/no <see cref="CheckboxQuestion"/>.</summary>
public class BooleanSubmissionAnswer : SubmissionAnswer
{
    public bool Checked { get; set; }
}

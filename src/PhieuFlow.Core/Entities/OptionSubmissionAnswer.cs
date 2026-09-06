namespace PhieuFlow.Core.Entities;

/// <summary>
/// One selected option of a <see cref="ChoiceQuestion"/>. DropDown / RadioButton produce a
/// single row; CheckBoxGroup produces one row per chosen option. Only the
/// <see cref="OptionId"/> is stored — the label lives on <see cref="QuestionOption"/>, and
/// the submission's published <see cref="FormVersion"/> is immutable (ADR 0007) so the
/// option always resolves.
/// </summary>
public class OptionSubmissionAnswer : SubmissionAnswer
{
    public Guid OptionId { get; set; }               // reference — deliberately NOT an FK
}

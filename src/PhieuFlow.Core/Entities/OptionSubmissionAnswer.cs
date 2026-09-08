namespace PhieuFlow.Core.Entities;

/// <summary>
/// One selected option of a <see cref="ChoiceQuestion"/>. A CheckBoxGroup makes one row per
/// selected option. Only <see cref="OptionId"/> is stored. The label resolves from the
/// published version, which never changes.
/// </summary>
public class OptionSubmissionAnswer : SubmissionAnswer
{
    public Guid OptionId { get; set; }               // a reference value, not a foreign key by design
}

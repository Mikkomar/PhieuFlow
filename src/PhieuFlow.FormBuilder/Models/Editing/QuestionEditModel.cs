namespace PhieuFlow.FormBuilder.Models.Editing;

/// <summary>
/// Client-side edit model for one question. Mirrors the <c>Question</c> entity hierarchy but
/// carries an <see cref="Issues"/> collection the builder renders inline.
/// </summary>
public abstract class QuestionEditModel : IHasIssues
{
    public required Guid Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public int Order { get; set; }
    public List<ValidationIssue> Issues { get; } = [];
    public virtual bool HasIssues => Issues.Count > 0;
}

public sealed class TextAreaQuestionEditModel : QuestionEditModel
{
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
}

public sealed class CheckboxQuestionEditModel : QuestionEditModel
{
    public string Label { get; set; } = string.Empty;
}

public abstract class ChoiceQuestionEditModel : QuestionEditModel
{
    public List<QuestionOptionEditModel> Options { get; set; } = [];
    public override bool HasIssues => Issues.Count > 0 || Options.Any(o => o.HasIssues);
}

public sealed class DropDownQuestionEditModel : ChoiceQuestionEditModel;

public sealed class RadioButtonQuestionEditModel : ChoiceQuestionEditModel;

public sealed class CheckBoxGroupQuestionEditModel : ChoiceQuestionEditModel
{
    public int? MinSelections { get; set; }
    public int? MaxSelections { get; set; }
}

public sealed class NumberQuestionEditModel : QuestionEditModel
{
    public decimal? Min { get; set; }
    public decimal? Max { get; set; }
}

public sealed class CalendarQuestionEditModel : QuestionEditModel
{
    public DateOnly? MinDate { get; set; }
    public DateOnly? MaxDate { get; set; }
}

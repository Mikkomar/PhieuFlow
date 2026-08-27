using System.Text.Json.Serialization;

namespace PhieuFlow.Hub.Contracts;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextAreaQuestionDto), "TextArea")]
[JsonDerivedType(typeof(CheckboxQuestionDto), "Checkbox")]
[JsonDerivedType(typeof(DropDownQuestionDto), "DropDown")]
[JsonDerivedType(typeof(RadioButtonQuestionDto), "RadioButton")]
[JsonDerivedType(typeof(CheckBoxGroupQuestionDto), "CheckBoxGroup")]
[JsonDerivedType(typeof(NumberQuestionDto), "Number")]
[JsonDerivedType(typeof(CalendarQuestionDto), "Calendar")]
public abstract class QuestionDto
{
    public required Guid Id { get; set; }
    public required string Text { get; set; }
    public required bool IsRequired { get; set; }
}

public class TextAreaQuestionDto : QuestionDto
{
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
}

public class CheckboxQuestionDto : QuestionDto
{
    public required string Label { get; set; }
}

public abstract class ChoiceQuestionDto : QuestionDto
{
    public required List<QuestionOptionDto> Options { get; set; }
}

public class DropDownQuestionDto : ChoiceQuestionDto
{
}

public class RadioButtonQuestionDto : ChoiceQuestionDto
{
}

public class CheckBoxGroupQuestionDto : ChoiceQuestionDto
{
    public int? MinSelections { get; set; }
    public int? MaxSelections { get; set; }
}

public class NumberQuestionDto : QuestionDto
{
    public decimal? Min { get; set; }
    public decimal? Max { get; set; }
}

public class CalendarQuestionDto : QuestionDto
{
    public DateOnly? MinDate { get; set; }
    public DateOnly? MaxDate { get; set; }
}

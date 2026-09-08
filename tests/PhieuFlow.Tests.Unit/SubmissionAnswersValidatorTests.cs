using AwesomeAssertions;
using PhieuFlow.Hub.Contracts.Forms;
using PhieuFlow.Hub.Contracts.Publishing;
using PhieuFlow.Hub.Contracts.Submissions;
using Xunit;

namespace PhieuFlow.Tests.Unit;

public class SubmissionAnswersValidatorTests
{
    private readonly SubmissionAnswersValidator _validator = new();

    [Fact]
    public void TestValidate_When_EveryAnswerSatisfiesItsConstraints_Should_ReturnNoErrors()
    {
        var text = TextArea(required: true, minLength: 2, maxLength: 10);
        var number = Number(required: true, min: 1, max: 5);
        var form = Form(Page(text, number));

        _validator.Validate(form, [Value(text, "hello"), Value(number, "3")]).Should().BeEmpty();
    }

    [Fact]
    public void TestValidate_When_RequiredTextAreaIsWhitespace_Should_FlagThatQuestion()
    {
        var text = TextArea(required: true);
        var form = Form(Page(text));

        _validator.Validate(form, [Value(text, "   ")])[text.Id].Should().Be("An answer is required.");
    }

    [Fact]
    public void TestValidate_When_OptionalTextAreaIsBlank_Should_ReturnNoErrors()
    {
        var text = TextArea(minLength: 5);
        var form = Form(Page(text));

        _validator.Validate(form, [Value(text, "")]).Should().BeEmpty();
    }

    [Fact]
    public void TestValidate_When_TextAreaShorterThanMinLength_Should_FlagThatQuestion()
    {
        var text = TextArea(minLength: 5);
        var form = Form(Page(text));

        _validator.Validate(form, [Value(text, "hi")])[text.Id].Should().Be("Enter at least 5 characters.");
    }

    [Fact]
    public void TestValidate_When_TextAreaLongerThanMaxLength_Should_FlagThatQuestion()
    {
        var text = TextArea(maxLength: 3);
        var form = Form(Page(text));

        _validator.Validate(form, [Value(text, "toolong")])[text.Id].Should().Be("Enter at most 3 characters.");
    }

    [Fact]
    public void TestValidate_When_TextAreaOutsideBothLengthBounds_Should_ReportTheRange()
    {
        var text = TextArea(minLength: 3, maxLength: 6);
        var form = Form(Page(text));

        _validator.Validate(form, [Value(text, "ab")])[text.Id].Should().Be("Enter between 3 and 6 characters.");
    }

    [Fact]
    public void TestValidate_When_RequiredNumberHasNoAnswer_Should_FlagThatQuestion()
    {
        var number = Number(required: true, min: 0);
        var form = Form(Page(number));

        _validator.Validate(form, [])[number.Id].Should().Be("An answer is required.");
    }

    [Fact]
    public void TestValidate_When_NumberIsNotParseable_Should_FlagThatQuestion()
    {
        var number = Number(min: 0, max: 10);
        var form = Form(Page(number));

        _validator.Validate(form, [Value(number, "abc")])[number.Id].Should().Be("Enter a valid number.");
    }

    [Fact]
    public void TestValidate_When_NumberBelowMin_Should_FlagThatQuestionWithTheRange()
    {
        var number = Number(min: 10, max: 20);
        var form = Form(Page(number));

        _validator.Validate(form, [Value(number, "4")])[number.Id].Should().Be("Enter a number between 10 and 20.");
    }

    [Fact]
    public void TestValidate_When_NumberAboveMaxOnly_Should_FlagThatQuestion()
    {
        var number = Number(max: 20);
        var form = Form(Page(number));

        _validator.Validate(form, [Value(number, "25")])[number.Id].Should().Be("Enter 20 or less.");
    }

    [Fact]
    public void TestValidate_When_NumberOnItsMaxBoundary_Should_ReturnNoErrors()
    {
        var number = Number(min: 1, max: 5);
        var form = Form(Page(number));

        _validator.Validate(form, [Value(number, "5")]).Should().BeEmpty();
    }

    [Fact]
    public void TestValidate_When_DateBeforeMinDate_Should_FlagThatQuestionWithTheRange()
    {
        var calendar = Calendar(min: new DateOnly(2026, 1, 1), max: new DateOnly(2026, 12, 31));
        var form = Form(Page(calendar));

        _validator.Validate(form, [Value(calendar, "2025-06-01")])[calendar.Id]
            .Should().Be("Pick a date between 2026-01-01 and 2026-12-31.");
    }

    [Fact]
    public void TestValidate_When_DateIsNotParseable_Should_FlagThatQuestion()
    {
        var calendar = Calendar(min: new DateOnly(2026, 1, 1));
        var form = Form(Page(calendar));

        _validator.Validate(form, [Value(calendar, "not-a-date")])[calendar.Id].Should().Be("Enter a valid date.");
    }

    [Fact]
    public void TestValidate_When_DateAfterMaxDateOnly_Should_FlagThatQuestion()
    {
        var calendar = Calendar(max: new DateOnly(2026, 1, 1));
        var form = Form(Page(calendar));

        _validator.Validate(form, [Value(calendar, "2026-02-01")])[calendar.Id]
            .Should().Be("Pick a date on or before 2026-01-01.");
    }

    [Fact]
    public void TestValidate_When_RequiredCheckboxIsUnchecked_Should_FlagThatQuestion()
    {
        var checkbox = Checkbox(required: true);
        var form = Form(Page(checkbox));

        _validator.Validate(form, [Bool(checkbox, false)])[checkbox.Id].Should().Be("An answer is required.");
    }

    [Fact]
    public void TestValidate_When_RequiredCheckboxIsChecked_Should_ReturnNoErrors()
    {
        var checkbox = Checkbox(required: true);
        var form = Form(Page(checkbox));

        _validator.Validate(form, [Bool(checkbox, true)]).Should().BeEmpty();
    }

    [Fact]
    public void TestValidate_When_OptionalCheckboxIsUnchecked_Should_ReturnNoErrors()
    {
        var checkbox = Checkbox();
        var form = Form(Page(checkbox));

        _validator.Validate(form, [Bool(checkbox, false)]).Should().BeEmpty();
    }

    [Fact]
    public void TestValidate_When_RequiredDropDownHasNoSelection_Should_FlagThatQuestion()
    {
        var dropDown = DropDown(required: true);
        var form = Form(Page(dropDown));

        _validator.Validate(form, [])[dropDown.Id].Should().Be("Select an option.");
    }

    [Fact]
    public void TestValidate_When_RequiredRadioHasSelection_Should_ReturnNoErrors()
    {
        var radio = Radio(required: true);
        var form = Form(Page(radio));

        _validator.Validate(form, [Option(radio, radio.Options[0].Id)]).Should().BeEmpty();
    }

    [Fact]
    public void TestValidate_When_RequiredCheckBoxGroupHasNoSelection_Should_FlagThatQuestion()
    {
        var group = Group(required: true, min: 1);
        var form = Form(Page(group));

        _validator.Validate(form, [])[group.Id].Should().Be("An answer is required.");
    }

    [Fact]
    public void TestValidate_When_CheckBoxGroupBelowMinSelections_Should_FlagThatQuestionWithTheRange()
    {
        var group = Group(min: 2, max: 3);
        var form = Form(Page(group));

        _validator.Validate(form, [Option(group, group.Options[0].Id)])[group.Id]
            .Should().Be("Choose between 2 and 3 options.");
    }

    [Fact]
    public void TestValidate_When_CheckBoxGroupAboveMaxSelectionsOnly_Should_FlagThatQuestion()
    {
        var group = Group(max: 1);
        var form = Form(Page(group));

        _validator.Validate(form, [Option(group, group.Options[0].Id), Option(group, group.Options[1].Id)])[group.Id]
            .Should().Be("Choose at most 1.");
    }

    [Fact]
    public void TestValidate_When_OptionalCheckBoxGroupHasNoSelection_Should_ReturnNoErrors()
    {
        var group = Group(min: 2);
        var form = Form(Page(group));

        _validator.Validate(form, []).Should().BeEmpty();
    }

    [Fact]
    public void TestValidate_When_SeveralQuestionsBreachConstraints_Should_FlagEachOfThem()
    {
        var text = TextArea(required: true);
        var number = Number(min: 0, max: 10);
        var form = Form(Page(text), Page(number));

        _validator.Validate(form, [Value(text, ""), Value(number, "50")])
            .Should().ContainKeys(text.Id, number.Id);
    }

    [Fact]
    public void TestValidate_When_ValidAnswersSpanMultiplePages_Should_ReturnNoErrors()
    {
        var first = TextArea(required: true);
        var second = Number(required: true, min: 1);
        var form = Form(Page(first), Page(second));

        _validator.Validate(form, [Value(first, "done"), Value(second, "9")]).Should().BeEmpty();
    }

    // ---- structural integrity (a stale or crafted message, never the client's UI) ---------

    [Fact]
    public void TestValidate_When_AnswerNamesAQuestionNotOnTheForm_Should_FlagIt()
    {
        var text = TextArea();
        var form = Form(Page(text));
        var stray = Guid.NewGuid();

        _validator.Validate(form, [new ValueAnswerDto { QuestionId = stray, QuestionText = "?", Order = 0, Value = "x" }])[stray]
            .Should().Be("This answer does not correspond to a question on the form.");
    }

    [Fact]
    public void TestValidate_When_AnswerTypeDoesNotMatchTheQuestion_Should_FlagThatQuestion()
    {
        var number = Number();
        var form = Form(Page(number));

        _validator.Validate(form, [Bool(number, true)])[number.Id]
            .Should().Be("The answer type does not match the question.");
    }

    [Fact]
    public void TestValidate_When_ChosenOptionIsNotOnTheQuestion_Should_FlagThatQuestion()
    {
        var radio = Radio(required: true);
        var form = Form(Page(radio));

        _validator.Validate(form, [Option(radio, Guid.NewGuid())])[radio.Id]
            .Should().Be("The selected option is not offered by this question.");
    }

    [Fact]
    public void TestValidate_When_SingleChoiceHasMoreThanOneSelection_Should_FlagThatQuestion()
    {
        var radio = Radio(required: true);
        var form = Form(Page(radio));

        _validator.Validate(form, [Option(radio, radio.Options[0].Id), Option(radio, radio.Options[1].Id)])[radio.Id]
            .Should().Be("Select only one option.");
    }

    [Fact]
    public void TestValidate_When_CheckBoxGroupRepeatsAnOption_Should_FlagThatQuestion()
    {
        var group = Group(min: 1, max: 3);
        var form = Form(Page(group));

        _validator.Validate(form, [Option(group, group.Options[0].Id), Option(group, group.Options[0].Id)])[group.Id]
            .Should().Be("Each option can only be chosen once.");
    }

    // ---- builders ------------------------------------------------------------------------

    private static PublishedFormDto Form(params FormPageDto[] pages) => new()
    {
        Id = Guid.NewGuid(),
        VersionNumber = 1,
        Title = "Survey",
        Pages = pages.ToList(),
    };

    private static FormPageDto Page(params QuestionDto[] questions) => new()
    {
        Id = Guid.NewGuid(),
        Title = null,
        Questions = questions.ToList(),
    };

    private static TextAreaQuestionDto TextArea(bool required = false, int? minLength = null, int? maxLength = null) => new()
    {
        Id = Guid.NewGuid(),
        Text = "Tell us",
        IsRequired = required,
        MinLength = minLength,
        MaxLength = maxLength,
    };

    private static NumberQuestionDto Number(bool required = false, decimal? min = null, decimal? max = null) => new()
    {
        Id = Guid.NewGuid(),
        Text = "How many",
        IsRequired = required,
        Min = min,
        Max = max,
    };

    private static CalendarQuestionDto Calendar(bool required = false, DateOnly? min = null, DateOnly? max = null) => new()
    {
        Id = Guid.NewGuid(),
        Text = "When",
        IsRequired = required,
        MinDate = min,
        MaxDate = max,
    };

    private static CheckboxQuestionDto Checkbox(bool required = false) => new()
    {
        Id = Guid.NewGuid(),
        Text = "Agree",
        IsRequired = required,
        Label = "I agree",
    };

    private static DropDownQuestionDto DropDown(bool required = false) => new()
    {
        Id = Guid.NewGuid(),
        Text = "Pick",
        IsRequired = required,
        Options = Options(2),
    };

    private static RadioButtonQuestionDto Radio(bool required = false) => new()
    {
        Id = Guid.NewGuid(),
        Text = "Pick one",
        IsRequired = required,
        Options = Options(2),
    };

    private static CheckBoxGroupQuestionDto Group(bool required = false, int? min = null, int? max = null, int optionCount = 4) => new()
    {
        Id = Guid.NewGuid(),
        Text = "Pick some",
        IsRequired = required,
        MinSelections = min,
        MaxSelections = max,
        Options = Options(optionCount),
    };

    private static List<QuestionOptionDto> Options(int count) => Enumerable.Range(0, count)
        .Select(i => new QuestionOptionDto { Id = Guid.NewGuid(), Label = $"Option {i}", Order = i })
        .ToList();

    private static ValueAnswerDto Value(QuestionDto question, string? value) => new()
    {
        QuestionId = question.Id,
        QuestionText = question.Text,
        Order = 0,
        Value = value,
    };

    private static BooleanAnswerDto Bool(QuestionDto question, bool @checked) => new()
    {
        QuestionId = question.Id,
        QuestionText = question.Text,
        Order = 0,
        Checked = @checked,
    };

    private static OptionAnswerDto Option(QuestionDto question, Guid optionId) => new()
    {
        QuestionId = question.Id,
        QuestionText = question.Text,
        Order = 0,
        OptionId = optionId,
    };
}

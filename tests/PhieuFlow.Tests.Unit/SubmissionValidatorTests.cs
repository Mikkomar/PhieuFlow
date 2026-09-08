using AwesomeAssertions;
using PhieuFlow.FormFiller.Validation;
using PhieuFlow.Hub.Contracts.Forms;
using PhieuFlow.Hub.Contracts.Publishing;
using Xunit;

namespace PhieuFlow.Tests.Unit;

public class SubmissionValidatorTests
{
    private readonly SubmissionValidator _validator = new();

    [Fact]
    public void TestValidate_When_EveryAnswerSatisfiesItsConstraints_Should_ReturnNoErrors()
    {
        var text = TextArea(required: true, minLength: 2, maxLength: 10);
        var number = Number(required: true, min: 1, max: 5);
        var form = Form(Page(text, number));
        var answers = new Dictionary<Guid, object?> { [text.Id] = "hello", [number.Id] = "3" };

        _validator.Validate(form, answers).Should().BeEmpty();
    }

    [Fact]
    public void TestValidate_When_RequiredTextAreaIsWhitespace_Should_FlagThatQuestion()
    {
        var text = TextArea(required: true);
        var form = Form(Page(text));

        _validator.Validate(form, Answer(text.Id, "   "))[text.Id].Should().Be("An answer is required.");
    }

    [Fact]
    public void TestValidate_When_OptionalTextAreaIsBlank_Should_ReturnNoErrors()
    {
        var text = TextArea(minLength: 5);
        var form = Form(Page(text));

        _validator.Validate(form, Answer(text.Id, "")).Should().BeEmpty();
    }

    [Fact]
    public void TestValidate_When_TextAreaShorterThanMinLength_Should_FlagThatQuestion()
    {
        var text = TextArea(minLength: 5);
        var form = Form(Page(text));

        _validator.Validate(form, Answer(text.Id, "hi"))[text.Id].Should().Be("Enter at least 5 characters.");
    }

    [Fact]
    public void TestValidate_When_TextAreaLongerThanMaxLength_Should_FlagThatQuestion()
    {
        var text = TextArea(maxLength: 3);
        var form = Form(Page(text));

        _validator.Validate(form, Answer(text.Id, "toolong"))[text.Id].Should().Be("Enter at most 3 characters.");
    }

    [Fact]
    public void TestValidate_When_TextAreaOutsideBothLengthBounds_Should_ReportTheRange()
    {
        var text = TextArea(minLength: 3, maxLength: 6);
        var form = Form(Page(text));

        _validator.Validate(form, Answer(text.Id, "ab"))[text.Id].Should().Be("Enter between 3 and 6 characters.");
    }

    [Fact]
    public void TestValidate_When_RequiredNumberIsBlank_Should_FlagThatQuestion()
    {
        var number = Number(required: true, min: 0);
        var form = Form(Page(number));

        _validator.Validate(form, Answer(number.Id, ""))[number.Id].Should().Be("An answer is required.");
    }

    [Fact]
    public void TestValidate_When_NumberIsNotParseable_Should_FlagThatQuestion()
    {
        var number = Number(min: 0, max: 10);
        var form = Form(Page(number));

        _validator.Validate(form, Answer(number.Id, "abc"))[number.Id].Should().Be("Enter a valid number.");
    }

    [Fact]
    public void TestValidate_When_NumberBelowMin_Should_FlagThatQuestionWithTheRange()
    {
        var number = Number(min: 10, max: 20);
        var form = Form(Page(number));

        _validator.Validate(form, Answer(number.Id, "4"))[number.Id].Should().Be("Enter a number between 10 and 20.");
    }

    [Fact]
    public void TestValidate_When_NumberAboveMaxOnly_Should_FlagThatQuestion()
    {
        var number = Number(max: 20);
        var form = Form(Page(number));

        _validator.Validate(form, Answer(number.Id, "25"))[number.Id].Should().Be("Enter 20 or less.");
    }

    [Fact]
    public void TestValidate_When_NumberOnItsMaxBoundary_Should_ReturnNoErrors()
    {
        var number = Number(min: 1, max: 5);
        var form = Form(Page(number));

        _validator.Validate(form, Answer(number.Id, "5")).Should().BeEmpty();
    }

    [Fact]
    public void TestValidate_When_DateBeforeMinDate_Should_FlagThatQuestionWithTheRange()
    {
        var calendar = Calendar(min: new DateOnly(2026, 1, 1), max: new DateOnly(2026, 12, 31));
        var form = Form(Page(calendar));

        _validator.Validate(form, Answer(calendar.Id, "2025-06-01"))[calendar.Id]
            .Should().Be("Pick a date between 2026-01-01 and 2026-12-31.");
    }

    [Fact]
    public void TestValidate_When_DateIsNotParseable_Should_FlagThatQuestion()
    {
        var calendar = Calendar(min: new DateOnly(2026, 1, 1));
        var form = Form(Page(calendar));

        _validator.Validate(form, Answer(calendar.Id, "not-a-date"))[calendar.Id].Should().Be("Enter a valid date.");
    }

    [Fact]
    public void TestValidate_When_DateAfterMaxDateOnly_Should_FlagThatQuestion()
    {
        var calendar = Calendar(max: new DateOnly(2026, 1, 1));
        var form = Form(Page(calendar));

        _validator.Validate(form, Answer(calendar.Id, "2026-02-01"))[calendar.Id]
            .Should().Be("Pick a date on or before 2026-01-01.");
    }

    [Fact]
    public void TestValidate_When_RequiredCheckboxIsUnchecked_Should_FlagThatQuestion()
    {
        var checkbox = Checkbox(required: true);
        var form = Form(Page(checkbox));

        _validator.Validate(form, Answer(checkbox.Id, false))[checkbox.Id].Should().Be("An answer is required.");
    }

    [Fact]
    public void TestValidate_When_RequiredCheckboxIsChecked_Should_ReturnNoErrors()
    {
        var checkbox = Checkbox(required: true);
        var form = Form(Page(checkbox));

        _validator.Validate(form, Answer(checkbox.Id, true)).Should().BeEmpty();
    }

    [Fact]
    public void TestValidate_When_OptionalCheckboxIsUnchecked_Should_ReturnNoErrors()
    {
        var checkbox = Checkbox();
        var form = Form(Page(checkbox));

        _validator.Validate(form, Answer(checkbox.Id, false)).Should().BeEmpty();
    }

    [Fact]
    public void TestValidate_When_RequiredDropDownHasNoSelection_Should_FlagThatQuestion()
    {
        var dropDown = DropDown(required: true);
        var form = Form(Page(dropDown));

        _validator.Validate(form, new Dictionary<Guid, object?>())[dropDown.Id].Should().Be("Select an option.");
    }

    [Fact]
    public void TestValidate_When_RequiredRadioHasSelection_Should_ReturnNoErrors()
    {
        var radio = Radio(required: true);
        var form = Form(Page(radio));

        _validator.Validate(form, Answer(radio.Id, radio.Options[0].Id)).Should().BeEmpty();
    }

    [Fact]
    public void TestValidate_When_RequiredCheckBoxGroupHasNoSelection_Should_FlagThatQuestion()
    {
        var group = Group(required: true, min: 1);
        var form = Form(Page(group));

        _validator.Validate(form, Answer(group.Id, new HashSet<Guid>()))[group.Id].Should().Be("An answer is required.");
    }

    [Fact]
    public void TestValidate_When_CheckBoxGroupBelowMinSelections_Should_FlagThatQuestionWithTheRange()
    {
        var group = Group(min: 2, max: 3);
        var form = Form(Page(group));
        var picked = new HashSet<Guid> { group.Options[0].Id };

        _validator.Validate(form, Answer(group.Id, picked))[group.Id].Should().Be("Choose between 2 and 3 options.");
    }

    [Fact]
    public void TestValidate_When_CheckBoxGroupAboveMaxSelectionsOnly_Should_FlagThatQuestion()
    {
        var group = Group(max: 1);
        var form = Form(Page(group));
        var picked = new HashSet<Guid> { group.Options[0].Id, group.Options[1].Id };

        _validator.Validate(form, Answer(group.Id, picked))[group.Id].Should().Be("Choose at most 1.");
    }

    [Fact]
    public void TestValidate_When_OptionalCheckBoxGroupHasNoSelection_Should_ReturnNoErrors()
    {
        var group = Group(min: 2);
        var form = Form(Page(group));

        _validator.Validate(form, Answer(group.Id, new HashSet<Guid>())).Should().BeEmpty();
    }

    [Fact]
    public void TestValidate_When_SeveralQuestionsBreachConstraints_Should_FlagEachOfThem()
    {
        var text = TextArea(required: true);
        var number = Number(min: 0, max: 10);
        var form = Form(Page(text), Page(number));
        var answers = new Dictionary<Guid, object?> { [text.Id] = "", [number.Id] = "50" };

        _validator.Validate(form, answers).Should().ContainKeys(text.Id, number.Id);
    }

    [Fact]
    public void TestValidate_When_ValidAnswersSpanMultiplePages_Should_ReturnNoErrors()
    {
        var first = TextArea(required: true);
        var second = Number(required: true, min: 1);
        var form = Form(Page(first), Page(second));
        var answers = new Dictionary<Guid, object?> { [first.Id] = "done", [second.Id] = "9" };

        _validator.Validate(form, answers).Should().BeEmpty();
    }

    // ---- builders --------------------------------------------------------------

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

    private static Dictionary<Guid, object?> Answer(Guid questionId, object? value) => new() { [questionId] = value };
}

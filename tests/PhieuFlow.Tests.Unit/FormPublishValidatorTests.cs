using AwesomeAssertions;
using PhieuFlow.Hub.Contracts;
using PhieuFlow.Hub.Validation;
using Xunit;
using static PhieuFlow.Tests.Unit.TreeBuilder;

namespace PhieuFlow.Tests.Unit;

public class FormPublishValidatorTests
{
    private readonly FormPublishValidator _validator = new();

    [Fact]
    public void TestValidate_When_FormIsComplete_Should_ReturnTrueAndLeaveTreeClean()
    {
        var form = Form("Onboarding", Page("Details", TextArea("Your name"), DropDown("Team", "Eng", "Design")));

        var publishable = _validator.Validate(form);

        publishable.Should().BeTrue();
        form.AllIssues().Should().BeEmpty();
    }

    [Fact]
    public void TestValidate_When_TitleEmpty_Should_AddTitleIssueToForm()
    {
        var form = Form("  ", Page(null, TextArea("Your name")));

        _validator.Validate(form).Should().BeFalse();

        form.Issues.Should().ContainSingle().Which.Field.Should().Be(ValidationField.Title);
    }

    [Fact]
    public void TestValidate_When_QuestionTextEmpty_Should_AddTextIssueToThatQuestion()
    {
        var blank = TextArea("");
        var form = Form("Survey", Page(null, TextArea("Filled in"), blank));

        _validator.Validate(form).Should().BeFalse();

        blank.Issues.Should().ContainSingle().Which.Field.Should().Be(ValidationField.Text);
        form.Pages[0].Questions[0].Issues.Should().BeEmpty();
    }

    [Fact]
    public void TestValidate_When_SinglePageHasNoQuestions_Should_AddPageIssue()
    {
        var form = Form("Survey", Page("Only page"));

        _validator.Validate(form).Should().BeFalse();

        form.Pages[0].Issues.Should().ContainSingle().Which.Field.Should().Be(ValidationField.None);
    }

    [Fact]
    public void TestValidate_When_MultiPageHasEmptyPage_Should_AddNoIssue()
    {
        var form = Form("Survey", Page("One", TextArea("Something")), Page("Two"));

        _validator.Validate(form).Should().BeTrue();

        form.AllIssues().Should().BeEmpty();
    }

    [Fact]
    public void TestValidate_When_ChoiceQuestionHasNoOptions_Should_AddOptionsIssue()
    {
        var dropdown = DropDown("Team");
        var form = Form("Survey", Page(null, dropdown));

        _validator.Validate(form).Should().BeFalse();

        dropdown.Issues.Should().ContainSingle().Which.Field.Should().Be(ValidationField.Options);
    }

    [Fact]
    public void TestValidate_When_OptionLabelEmpty_Should_AddLabelIssueToThatOption()
    {
        var dropdown = DropDown("Team", "Eng", "  ");
        var form = Form("Survey", Page(null, dropdown));

        _validator.Validate(form).Should().BeFalse();

        dropdown.Options[0].Issues.Should().BeEmpty();
        dropdown.Options[1].Issues.Should().ContainSingle().Which.Field.Should().Be(ValidationField.Label);
    }

    [Fact]
    public void TestValidate_When_OptionLabelsDifferByCaseOnly_Should_AddIssuePerOffendingOption()
    {
        var dropdown = DropDown("Team", "Design", "design");
        var form = Form("Survey", Page(null, dropdown));

        _validator.Validate(form).Should().BeFalse();

        dropdown.Options[0].Issues.Should().BeEmpty();
        dropdown.Options[1].Issues.Should().ContainSingle().Which.Field.Should().Be(ValidationField.Label);
    }

    [Fact]
    public void TestValidate_When_CheckboxLabelEmpty_Should_AddLabelIssueToThatQuestion()
    {
        var checkbox = Checkbox("Terms", "  ");
        var form = Form("Survey", Page(null, checkbox));

        _validator.Validate(form).Should().BeFalse();

        checkbox.Issues.Should().ContainSingle().Which.Field.Should().Be(ValidationField.Label);
    }

    [Fact]
    public void TestValidate_When_NumberMinExceedsMax_Should_AddMinIssue()
    {
        var number = Number("Age", min: 40, max: 10);
        var form = Form("Survey", Page(null, number));

        _validator.Validate(form).Should().BeFalse();

        number.Issues.Should().ContainSingle().Which.Field.Should().Be(ValidationField.Min);
    }

    [Fact]
    public void TestValidate_When_MinSelectionsExceedOptionCount_Should_AddMinSelectionsIssue()
    {
        var group = CheckBoxGroup("Pick", minSelections: 3, maxSelections: null, "A", "B");
        var form = Form("Survey", Page(null, group));

        _validator.Validate(form).Should().BeFalse();

        group.Issues.Should().Contain(i => i.Field == ValidationField.MinSelections);
    }

    [Fact]
    public void TestValidate_When_CalledTwice_Should_ClearStaleIssuesFromTheFirstRun()
    {
        var blank = TextArea("");
        var form = Form("Survey", Page(null, blank));

        _validator.Validate(form);
        blank.Text = "Now filled in";
        var publishable = _validator.Validate(form);

        publishable.Should().BeTrue();
        form.AllIssues().Should().BeEmpty();
    }
}

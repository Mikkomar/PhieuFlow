using AwesomeAssertions;
using PhieuFlow.FormBuilder.Models.Editing;
using PhieuFlow.Hub.Contracts;
using Xunit;

namespace PhieuFlow.Tests.Unit;

public class HasIssuesExtensionsTests
{
    [Fact]
    public void TestEdit_When_NodeHasIssues_Should_ClearThemAfterApplyingTheMutation()
    {
        var question = new TextAreaQuestionEditModel { Id = Guid.NewGuid(), Text = string.Empty };
        question.Issues.Add(new ValidationIssue("Text is required", ValidationField.Text));

        question.Edit(() => question.Text = "How did you hear about us?");

        question.Text.Should().Be("How did you hear about us?");
        question.Issues.Should().BeEmpty();
    }

    [Fact]
    public void TestEdit_When_ASiblingNodeIsEdited_Should_LeaveThisNodesIssuesUntouched()
    {
        var edited = new QuestionOptionEditModel { Id = Guid.NewGuid(), Label = "Red" };
        var sibling = new QuestionOptionEditModel { Id = Guid.NewGuid(), Label = "Red" };
        edited.Issues.Add(new ValidationIssue("Duplicate label", ValidationField.Label));
        sibling.Issues.Add(new ValidationIssue("Duplicate label", ValidationField.Label));

        edited.Edit(() => edited.Label = "Blue");

        edited.Issues.Should().BeEmpty();
        sibling.Issues.Should().ContainSingle().Which.Message.Should().Be("Duplicate label");
    }
}

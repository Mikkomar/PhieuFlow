using AwesomeAssertions;
using PhieuFlow.FormBuilder.Models.Editing;
using PhieuFlow.Hub.Contracts;
using Xunit;

namespace PhieuFlow.Tests.Unit;

public class FormEditMapperTests
{
    [Fact]
    public void TestRoundTrip_When_TreeHasEveryQuestionType_Should_PreserveStructureAndIds()
    {
        var original = SampleForm();

        var restored = FormEditMapper.ToEditModel(FormEditMapper.ToDto(original));

        restored.FormId.Should().Be(original.FormId);
        restored.Pages.Should().HaveCount(original.Pages.Count);

        var restoredTypes = restored.Pages[0].Questions.Select(q => q.GetType().Name);
        var originalTypes = original.Pages[0].Questions.Select(q => q.GetType().Name);
        restoredTypes.Should().Equal(originalTypes);

        var restoredDropdown = restored.Pages[0].Questions.OfType<DropDownQuestionEditModel>().Single();
        var originalDropdown = original.Pages[0].Questions.OfType<DropDownQuestionEditModel>().Single();
        restoredDropdown.Id.Should().Be(originalDropdown.Id);
        restoredDropdown.Options.Select(o => o.Label).Should().Equal(originalDropdown.Options.Select(o => o.Label));
        restoredDropdown.Options.Select(o => o.Order).Should().Equal(0, 1, 2);
    }

    [Fact]
    public void TestApplyIssues_When_AnnotatedTreeReturned_Should_CopyIssuesOntoMatchingNodesOnly()
    {
        var form = SampleForm();
        var annotated = FormEditMapper.ToDto(form);

        annotated.Issues.Add(new ValidationIssueDto { Message = "no title", Field = ValidationField.Title });
        var annotatedDropdown = annotated.Pages[0].Questions.OfType<DropDownQuestionDto>().Single();
        annotatedDropdown.Options[1].Issues.Add(new ValidationIssueDto { Message = "dup", Field = ValidationField.Label });

        FormEditMapper.ApplyIssues(form, annotated);

        form.Issues.Should().ContainSingle().Which.Field.Should().Be(ValidationField.Title);
        var dropdown = form.Pages[0].Questions.OfType<DropDownQuestionEditModel>().Single();
        dropdown.Options[0].Issues.Should().BeEmpty();
        dropdown.Options[1].Issues.Should().ContainSingle().Which.Message.Should().Be("dup");
        form.HasIssues.Should().BeTrue();
    }

    [Fact]
    public void TestApplyIssues_When_CalledAgainWithCleanTree_Should_ClearPreviousIssues()
    {
        var form = SampleForm();
        form.Issues.Add(new ValidationIssue("stale", ValidationField.Title));

        FormEditMapper.ApplyIssues(form, FormEditMapper.ToDto(SampleForm()));

        form.HasIssues.Should().BeFalse();
    }

    private static FormEditModel SampleForm()
    {
        var form = new FormEditModel { FormId = Guid.NewGuid(), Title = "Sample" };
        var page = new FormPageEditModel { Id = Guid.NewGuid(), Title = "Page 1", Order = 0 };

        page.Questions.Add(new TextAreaQuestionEditModel { Id = Guid.NewGuid(), Text = "About you", Order = 0 });
        page.Questions.Add(new DropDownQuestionEditModel
        {
            Id = Guid.NewGuid(),
            Text = "Team",
            Order = 1,
            Options =
            {
                new QuestionOptionEditModel { Id = Guid.NewGuid(), Label = "Eng", Order = 0 },
                new QuestionOptionEditModel { Id = Guid.NewGuid(), Label = "Design", Order = 1 },
                new QuestionOptionEditModel { Id = Guid.NewGuid(), Label = "Ops", Order = 2 },
            },
        });
        page.Questions.Add(new NumberQuestionEditModel { Id = Guid.NewGuid(), Text = "Years", Order = 2, Min = 0, Max = 40 });
        page.Questions.Add(new CalendarQuestionEditModel { Id = Guid.NewGuid(), Text = "Start", Order = 3 });

        form.Pages.Add(page);
        return form;
    }
}

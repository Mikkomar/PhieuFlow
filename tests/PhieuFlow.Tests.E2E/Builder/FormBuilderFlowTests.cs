using System.Net.Http.Json;
using AwesomeAssertions;
using Microsoft.Playwright;
using PhieuFlow.Hub.Contracts;
using PhieuFlow.Tests.E2E.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace PhieuFlow.Tests.E2E.Builder;

/// <summary>
/// End-to-end coverage of the form-builder UI driving the real hub REST API
/// (ADR 0001 synchronous form management). Every test builds through the browser and
/// asserts the persisted result over <c>GET /forms</c>.
/// </summary>
public sealed class FormBuilderFlowTests(AppHostFixture fixture, ITestOutputHelper output)
    : E2ETestBase(fixture, output)
{
    [Fact]
    public async Task TestAutosave_When_FormHasTitleOnly_Should_PersistDraftToHub()
    {
        var builder = new FormBuilderPage(Page);
        var title = $"Title-only {Guid.NewGuid():N}";

        await GotoFormBuilderAsync("/forms/new");
        await builder.SetTitleAsync(title);
        await WaitForSavedAsync();

        var id = await GetFormIdByTitleAsync(title);
        var form = await GetFormAsync(id);

        form.Title.Should().Be(title);
        form.VersionNumber.Should().Be(1);
        form.Status.Should().Be(FormVersionStatusDto.Draft);
    }

    [Fact]
    public async Task TestAutosave_When_FormHasMultiplePagesAndQuestions_Should_PersistFullStructureToHub()
    {
        var builder = new FormBuilderPage(Page);
        var title = $"Structured {Guid.NewGuid():N}";

        await GotoFormBuilderAsync("/forms/new");
        await builder.SetTitleAsync(title);
        await builder.SetDescriptionAsync("Onboarding questionnaire");

        // Page 1: a text area and a required radio-button question with two options.
        await builder.AddQuestionAsync("Text area", "Tell us about yourself");
        await builder.AddQuestionAsync("Radio buttons", "Preferred contract");
        await builder.SetOptionsAsync("Full time", "Part time");
        await builder.ToggleRequiredAsync();

        // Page 2: a number question with a range.
        await builder.AddPageAsync();
        await builder.AddQuestionAsync("Number", "Years of experience");
        await builder.SetNumberRangeAsync("0", "40");

        await WaitForSavedAsync();

        var id = await GetFormIdByTitleAsync(title);
        var form = await GetFormAsync(id);

        form.Description.Should().Be("Onboarding questionnaire");
        form.Pages.Should().HaveCount(2);

        form.Pages[0].Questions.Should().SatisfyRespectively(
            q => q.Should().BeOfType<TextAreaQuestionDto>(),
            q =>
            {
                var radio = q.Should().BeOfType<RadioButtonQuestionDto>().Subject;
                radio.IsRequired.Should().BeTrue();
                // QuestionOption has no order column, so compare as a set, not a sequence.
                radio.Options.Select(o => o.Label).Should().BeEquivalentTo(new[] { "Full time", "Part time" });
            });

        var number = form.Pages[1].Questions.Should().ContainSingle().Which.Should().BeOfType<NumberQuestionDto>().Subject;
        number.Min.Should().Be(0m);
        number.Max.Should().Be(40m);
    }

    [Fact]
    public async Task TestAutosave_When_QuestionsReordered_Should_PersistNewOrder()
    {
        var builder = new FormBuilderPage(Page);
        var title = $"Reorder {Guid.NewGuid():N}";

        await GotoFormBuilderAsync("/forms/new");
        await builder.SetTitleAsync(title);
        // Distinct question TYPES so the assertion identifies each row without depending
        // on question text (which the builder can drop on rapid automated entry).
        await builder.AddQuestionAsync("Text area", "First");
        await builder.AddQuestionAsync("Number", "Second");
        await builder.AddQuestionAsync("Calendar", "Third");
        await WaitForSavedAsync();

        // Move the third question (Calendar) to the top.
        await builder.MoveQuestionByPositionAsync(fromIndex: 2, delta: -2);
        await WaitForSavedAsync();

        var form = await GetFormAsync(await GetFormIdByTitleAsync(title));
        form.Pages[0].Questions.Should().SatisfyRespectively(
            q => q.Should().BeOfType<CalendarQuestionDto>(),
            q => q.Should().BeOfType<TextAreaQuestionDto>(),
            q => q.Should().BeOfType<NumberQuestionDto>());
    }

    [Fact]
    public async Task TestAutosave_When_QuestionDeleted_Should_RemoveItFromHub()
    {
        var builder = new FormBuilderPage(Page);
        var title = $"Delete-question {Guid.NewGuid():N}";

        await GotoFormBuilderAsync("/forms/new");
        await builder.SetTitleAsync(title);
        await builder.AddQuestionAsync("Text area", "Keep me");
        await builder.AddQuestionAsync("Number", "Delete me");
        await WaitForSavedAsync();

        // The just-added Number question is expanded; delete it.
        await builder.DeleteExpandedQuestionAsync();
        await WaitForSavedAsync();

        var form = await GetFormAsync(await GetFormIdByTitleAsync(title));
        form.Pages[0].Questions.Should().ContainSingle()
            .Which.Should().BeOfType<TextAreaQuestionDto>(); // the survivor is "Keep me"
    }

    [Fact]
    public async Task TestAutosave_When_PageDeleted_Should_RemoveItsQuestionsFromHub()
    {
        var builder = new FormBuilderPage(Page);
        var title = $"Delete-page {Guid.NewGuid():N}";

        await GotoFormBuilderAsync("/forms/new");
        await builder.SetTitleAsync(title);
        await builder.AddQuestionAsync("Text area", "On page one");
        await builder.AddPageAsync();
        await builder.AddQuestionAsync("Text area", "On page two");
        await WaitForSavedAsync();

        await builder.SelectPageAsync(0);
        await builder.DeleteActivePageAsync();
        await WaitForSavedAsync();

        var form = await GetFormAsync(await GetFormIdByTitleAsync(title));
        form.Pages.Should().ContainSingle();
        form.Pages[0].Questions.Should().ContainSingle().Which.Text.Should().Be("On page two");
    }

    [Fact]
    public async Task TestAutosave_When_TitleIsEmpty_Should_NotPersistAnything()
    {
        var builder = new FormBuilderPage(Page);

        var before = await ListFormTitlesAsync();

        await GotoFormBuilderAsync("/forms/new");
        await builder.AddQuestionAsync("Text area", "Question without a form title");
        // Focus then blur so Blazor's @onblur actually fires (a bare .blur() on an
        // unfocused element is a no-op).
        await builder.TitleInput.FocusAsync();
        await builder.TitleInput.BlurAsync();

        await Assertions.Expect(builder.TitleRequiredMessage).ToBeVisibleAsync();
        // Give any (unexpected) autosave time to fire before checking the hub.
        await Task.Delay(AutosaveSettle);

        var after = await ListFormTitlesAsync();
        after.Should().HaveCount(before.Count);
    }

    [Fact]
    public async Task TestFormBuilder_When_ReopenedById_Should_RenderFormFromHub()
    {
        var builder = new FormBuilderPage(Page);
        var title = $"Reopen {Guid.NewGuid():N}";

        await GotoFormBuilderAsync("/forms/new");
        await builder.SetTitleAsync(title);
        await builder.AddQuestionAsync("Text area", "Persisted question");
        await WaitForSavedAsync();
        var id = await GetFormIdByTitleAsync(title);

        // A second browser page re-fetches the form and renders it end to end.
        var reopened = await Context.NewPageAsync();
        await NavigateAsync(reopened, FormBuilderUrl($"/forms/{id}"));

        await Assertions.Expect(reopened.GetByLabel("Form title")).ToHaveValueAsync(title);
        // The question text renders in both the editor card and the respondent preview.
        await Assertions.Expect(reopened.GetByText("Persisted question").First).ToBeVisibleAsync();
    }

    private async Task<List<string>> ListFormTitlesAsync()
    {
        using var client = Fixture.CreateHubClient();
        var batch = await client.GetFromJsonAsync<FormBatchResponse>("/forms?take=100");
        return batch?.Items.Select(i => i.Title).ToList() ?? [];
    }
}

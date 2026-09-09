using System.Net.Http.Json;
using AwesomeAssertions;
using Microsoft.Playwright;
using PhieuFlow.Hub.Contracts.Submissions;
using PhieuFlow.Tests.E2E.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace PhieuFlow.Tests.E2E.Submission;

/// <summary>
/// The primary submission scenario and its variants. The test builds and publishes a form,
/// fills it in a second browser context, then reads the stored submission from the hub.
/// </summary>
public sealed class SubmissionFlowTests(AppHostFixture fixture, ITestOutputHelper output)
    : E2ETestBase(fixture, output)
{
    [Fact]
    public async Task TestSubmit_When_AllQuestionTypesAnswered_Should_PersistSubmissionToHub()
    {
        // Context A: build and publish a form with several question types.
        var builder = new FormBuilderPage(Page);
        var title = $"Submit-all-types {Guid.NewGuid():N}";
        await GotoFormBuilderAsync("/forms/new");
        await builder.SetTitleAsync(title);
        // Save between each add. A new card collapses the previous one, which can drop its
        // unsaved question text and fail the publish gate.
        await builder.AddQuestionAsync("Text area", "Free text");
        await WaitForSavedAsync();
        await builder.AddQuestionAsync("Number", "A number");
        await WaitForSavedAsync();
        await builder.AddQuestionAsync("Radio buttons", "Pick one");
        await builder.SetOptionsAsync("Alpha", "Beta");
        await WaitForSavedAsync();
        await ClickPublishAsync();
        var id = await GetFormIdByTitleAsync(title);

        // Context B: the respondent fills the form and sends it.
        var filler = await Context.NewPageAsync();
        await NavigateAsync(filler, new Uri(Fixture.FormFillerBaseUrl!, $"/forms/{id}").ToString());
        await filler.GetByRole(AriaRole.Textbox).FillAsync("Some prose");
        await filler.GetByRole(AriaRole.Spinbutton).FillAsync("42");
        await filler.GetByRole(AriaRole.Radio, new() { Name = "Alpha" }).CheckAsync();
        await filler.GetByRole(AriaRole.Button, new() { Name = "Submit" }).ClickAsync();
        await Assertions.Expect(filler.GetByText("received")).ToBeVisibleAsync();

        // The hub receives the submission from the RabbitMQ queue.
        var submissions = await PollForSubmissionsAsync(id);
        submissions.Should().ContainSingle();
        var answers = submissions[0].Answers.ToDictionary(a => a.QuestionText, a => a.Value);
        answers["Free text"].Should().Be("Some prose");
        answers["A number"].Should().Be("42");
        answers["Pick one"].Should().Be("Alpha");
    }

    [Fact]
    public async Task TestSubmit_When_FormForkedAfterLoad_Should_ReferencePublishedVersionAtLoadTime()
    {
        var builder = new FormBuilderPage(Page);
        var title = $"Submit-versioned {Guid.NewGuid():N}";
        await GotoFormBuilderAsync("/forms/new");
        await builder.SetTitleAsync(title);
        await builder.AddQuestionAsync("Text area", "Q");
        await WaitForSavedAsync();
        await ClickPublishAsync();
        var id = await GetFormIdByTitleAsync(title);
        var publishedV1 = await GetFormAsync(id);

        // The respondent loads the published v1.
        var filler = await Context.NewPageAsync();
        await NavigateAsync(filler, new Uri(Fixture.FormFillerBaseUrl!, $"/forms/{id}").ToString());
        await filler.GetByRole(AriaRole.Textbox).FillAsync("answer");

        // The builder forks v2 before the respondent sends the form.
        await builder.SetTitleAsync($"{title} (v2)");
        await WaitForSavedAsync();

        await filler.GetByRole(AriaRole.Button, new() { Name = "Submit" }).ClickAsync();
        await Assertions.Expect(filler.GetByText("received")).ToBeVisibleAsync();

        var submissions = await PollForSubmissionsAsync(id);
        submissions[0].FormVersionNumber.Should().Be(publishedV1.VersionNumber); // v1, not the forked v2
    }

    [Fact]
    public async Task TestSubmit_When_FormHasNoPublishedVersion_Should_RejectSubmission()
    {
        var builder = new FormBuilderPage(Page);
        var title = $"Submit-unpublished {Guid.NewGuid():N}";
        await GotoFormBuilderAsync("/forms/new");
        await builder.SetTitleAsync(title);
        await builder.AddQuestionAsync("Text area", "Q");
        await WaitForSavedAsync();
        var id = await GetFormIdByTitleAsync(title);

        // The respondent cannot fill a draft-only form. The form-filler shows an unavailable
        // notice and no Submit button.
        var filler = await Context.NewPageAsync();
        await NavigateAsync(filler, new Uri(Fixture.FormFillerBaseUrl!, $"/forms/{id}").ToString());

        await Assertions.Expect(filler.GetByText("This form isn't available")).ToBeVisibleAsync();
        await Assertions.Expect(filler.GetByRole(AriaRole.Button, new() { Name = "Submit" })).Not.ToBeVisibleAsync();
    }

    private async Task<IReadOnlyList<SubmissionListItemDto>> PollForSubmissionsAsync(Guid formId, int attempts = 20)
    {
        using var client = await Fixture.CreateAuthorizedHubClientAsync("submissions:read");
        for (var i = 0; i < attempts; i++)
        {
            var response = await client.GetAsync($"/forms/{formId}/submissions");
            if (response.IsSuccessStatusCode)
            {
                var batch = await response.Content.ReadFromJsonAsync<SubmissionBatchResponse>();
                if (batch is { Items.Count: > 0 })
                {
                    return batch.Items;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        throw new TimeoutException($"No submission for form {formId} arrived at the hub.");
    }
}

using System.Net.Http.Json;
using AwesomeAssertions;
using Microsoft.Playwright;
using PhieuFlow.Hub.Contracts.Submissions;
using PhieuFlow.Tests.E2E.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace PhieuFlow.Tests.E2E.Submission;

/// <summary>
/// The form-filler validates answers against the form copy it loaded. The hub validates them
/// again against the version the response names, not against any later, stricter version.
/// </summary>
public sealed class SubmissionStalenessTests(AppHostFixture fixture, ITestOutputHelper output)
    : E2ETestBase(fixture, output)
{
    [Fact]
    public async Task TestSubmit_When_ConstraintTightenedByNewerVersionAfterLoad_Should_PersistAgainstLoadedVersion()
    {
        var builder = new FormBuilderPage(Page);
        var title = $"Stale {Guid.NewGuid():N}";
        await GotoFormBuilderAsync("/forms/new");
        await builder.SetTitleAsync(title);
        await builder.AddQuestionAsync("Text area", "Optional note"); // optional in v1
        await WaitForSavedAsync();
        await ClickPublishAsync();
        var id = await GetFormIdByTitleAsync(title);

        // The respondent loads the published v1. The optional field stays blank.
        var filler = await Context.NewPageAsync();
        await NavigateAsync(filler, new Uri(Fixture.FormFillerBaseUrl, $"/forms/{id}").ToString());

        // The owner makes the question required. That forks a v2 draft. A second publish
        // makes v2 the current published version.
        await builder.OpenQuestionAsync("Optional note");
        await builder.ToggleRequiredAsync();
        await WaitForSavedAsync();
        await ClickPublishAsync();

        // The respondent sends the copy they loaded. The form-filler validates it against v1,
        // where the field is optional, so the blank answer passes.
        await filler.GetByRole(AriaRole.Button, new() { Name = "Submit" }).ClickAsync();
        await Assertions.Expect(filler.GetByText("received")).ToBeVisibleAsync();

        // The hub validates the response against v1, the version the respondent used, not
        // against the stricter v2. It stores the response and links it to v1.
        var submissions = await PollForSubmissionsAsync(id);
        submissions.Should().ContainSingle();
        submissions[0].FormVersionNumber.Should().Be(1);
    }

    [Fact]
    public async Task TestSubmit_When_FilledAgainstCurrentVersion_Should_PersistWithAnswer()
    {
        var builder = new FormBuilderPage(Page);
        var title = $"Fresh {Guid.NewGuid():N}";
        await GotoFormBuilderAsync("/forms/new");
        await builder.SetTitleAsync(title);
        await builder.AddQuestionAsync("Text area", "Note");
        await WaitForSavedAsync();
        await ClickPublishAsync();
        var id = await GetFormIdByTitleAsync(title);

        var filler = await Context.NewPageAsync();
        await NavigateAsync(filler, new Uri(Fixture.FormFillerBaseUrl, $"/forms/{id}").ToString());
        await filler.GetByRole(AriaRole.Textbox).FillAsync("all good");
        await filler.GetByRole(AriaRole.Button, new() { Name = "Submit" }).ClickAsync();
        await Assertions.Expect(filler.GetByText("received")).ToBeVisibleAsync();

        var submissions = await PollForSubmissionsAsync(id);
        submissions.Should().ContainSingle();
        submissions[0].FormVersionNumber.Should().Be(1);
        submissions[0].Answers.Should().ContainSingle(a => a.QuestionText == "Note" && a.Value == "all good");
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

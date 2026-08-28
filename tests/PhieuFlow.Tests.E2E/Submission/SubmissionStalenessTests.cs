using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;
using PhieuFlow.Tests.E2E.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace PhieuFlow.Tests.E2E.Submission;

/// <summary>
/// ADR 0002: the form-filler validates against the copy it loaded; the hub re-validates
/// on consume against the current form and, on a revision mismatch, flags the submission
/// for review rather than silently accepting or discarding it.
///
/// Skipped until hub-side re-validation and the review flag exist.
/// </summary>
public sealed class SubmissionStalenessTests(AppHostFixture fixture, ITestOutputHelper output)
    : E2ETestBase(fixture, output)
{
    private const string Blocker =
        "hub re-validation on consume + flag-for-review on revision mismatch not implemented — ADR 0002";

    [Fact(Skip = Blocker)]
    [Trait("Category", "Future")]
    public async Task TestSubmit_When_RevisionStaleOnConsume_Should_FlagSubmissionForReview()
    {
        var builder = new FormBuilderPage(Page);
        var title = $"Stale {Guid.NewGuid():N}";
        await GotoFormBuilderAsync("/forms/new");
        await builder.SetTitleAsync(title);
        await builder.AddQuestionAsync("Text area", "Optional note"); // starts optional
        await WaitForSavedAsync();
        await ClickPublishAsync();
        var id = await GetFormIdByTitleAsync(title);

        // Respondent loads the form at its current revision and leaves the field blank.
        var filler = await Context.NewPageAsync();
        await filler.GotoAsync(new Uri(Fixture.FormFillerBaseUrl!, $"/f/{id}").ToString());

        // Owner tightens the constraint (optional -> required) and republishes.
        await builder.OpenQuestionAsync("Optional note");
        await builder.ToggleRequiredAsync();
        await WaitForSavedAsync();
        await ClickPublishAsync();

        // Respondent submits the stale copy, which passed its (now outdated) validation.
        await filler.GetByRole(AriaRole.Button, new() { Name = "Submit" }).ClickAsync();
        await Assertions.Expect(filler.GetByText("received")).ToBeVisibleAsync();

        var submission = await PollForFirstSubmissionAsync(id);
        submission.GetProperty("status").GetString().Should().Be("FlaggedForReview");
    }

    [Fact(Skip = Blocker)]
    [Trait("Category", "Future")]
    public async Task TestSubmit_When_RevisionCurrentOnConsume_Should_AcceptWithoutFlag()
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
        await filler.GotoAsync(new Uri(Fixture.FormFillerBaseUrl!, $"/f/{id}").ToString());
        await filler.GetByLabel("Note").FillAsync("all good");
        await filler.GetByRole(AriaRole.Button, new() { Name = "Submit" }).ClickAsync();
        await Assertions.Expect(filler.GetByText("received")).ToBeVisibleAsync();

        var submission = await PollForFirstSubmissionAsync(id);
        submission.GetProperty("status").GetString().Should().Be("Accepted");
    }

    private async Task<JsonElement> PollForFirstSubmissionAsync(Guid formId, int attempts = 20)
    {
        using var client = Fixture.CreateHubClient();
        for (var i = 0; i < attempts; i++)
        {
            var response = await client.GetAsync($"/forms/{formId}/submissions");
            if (response.IsSuccessStatusCode)
            {
                var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
                if (doc.ValueKind == JsonValueKind.Array && doc.GetArrayLength() > 0)
                {
                    return doc[0];
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        throw new TimeoutException($"No submission for form {formId} arrived at the hub.");
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;
using PhieuFlow.Tests.E2E.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace PhieuFlow.Tests.E2E.Submission;

/// <summary>
/// The primary submission scenario and its variants: a form is built and published in one
/// browser context, filled in another, and the hub is asserted to have persisted the
/// submission. Skipped until <c>GET /forms/{id}/submissions</c> exists to read it back.
/// </summary>
public sealed class SubmissionFlowTests(AppHostFixture fixture, ITestOutputHelper output)
    : E2ETestBase(fixture, output)
{
    private const string Blocker =
        "GET /forms/{id}/submissions not implemented — the consumer persists (ADR 0009) but nothing reads it back — ADR 0001/0006/0008";

    [Fact(Skip = Blocker)]
    [Trait("Category", "Future")]
    public async Task TestSubmit_When_AllQuestionTypesAnswered_Should_PersistSubmissionToHub()
    {
        // Context A: build and publish a form covering every question type.
        var builder = new FormBuilderPage(Page);
        var title = $"Submit-all-types {Guid.NewGuid():N}";
        await GotoFormBuilderAsync("/forms/new");
        await builder.SetTitleAsync(title);
        await builder.AddQuestionAsync("Text area", "Free text");
        await builder.AddQuestionAsync("Number", "A number");
        await builder.AddQuestionAsync("Radio buttons", "Pick one");
        await builder.SetOptionsAsync("Alpha", "Beta");
        await WaitForSavedAsync();
        await ClickPublishAsync();
        var id = await GetFormIdByTitleAsync(title);

        // Context B: the respondent fills and submits.
        var filler = await Context.NewPageAsync();
        await filler.GotoAsync(new Uri(Fixture.FormFillerBaseUrl!, $"/forms/{id}").ToString());
        await filler.GetByLabel("Free text").FillAsync("Some prose");
        await filler.GetByLabel("A number").FillAsync("42");
        await filler.GetByRole(AriaRole.Radio, new() { Name = "Alpha" }).CheckAsync();
        await filler.GetByRole(AriaRole.Button, new() { Name = "Submit" }).ClickAsync();
        await Assertions.Expect(filler.GetByText("received")).ToBeVisibleAsync();

        // Hub: the submission arrives after the RabbitMQ round-trip.
        var submission = await PollForFirstSubmissionAsync(id);
        var answers = submission.GetProperty("answers").EnumerateArray()
            .ToDictionary(a => a.GetProperty("questionText").GetString()!, a => a.GetProperty("value").GetString());
        answers["Free text"].Should().Be("Some prose");
        answers["A number"].Should().Be("42");
        answers["Pick one"].Should().Be("Alpha");
    }

    [Fact(Skip = Blocker)]
    [Trait("Category", "Future")]
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

        // Respondent loads published v1.
        var filler = await Context.NewPageAsync();
        await filler.GotoAsync(new Uri(Fixture.FormFillerBaseUrl!, $"/forms/{id}").ToString());
        await filler.GetByLabel("Q").FillAsync("answer");

        // Builder forks v2 before the respondent submits.
        await builder.SetTitleAsync($"{title} (v2)");
        await WaitForSavedAsync();

        await filler.GetByRole(AriaRole.Button, new() { Name = "Submit" }).ClickAsync();
        await Assertions.Expect(filler.GetByText("received")).ToBeVisibleAsync();

        var submission = await PollForFirstSubmissionAsync(id);
        var referencedVersion = submission.GetProperty("formVersionNumber").GetInt32();
        referencedVersion.Should().Be(publishedV1.VersionNumber); // v1, not the forked v2
    }

    [Fact(Skip = Blocker)]
    [Trait("Category", "Future")]
    public async Task TestSubmit_When_FormHasNoPublishedVersion_Should_RejectSubmission()
    {
        var builder = new FormBuilderPage(Page);
        var title = $"Submit-unpublished {Guid.NewGuid():N}";
        await GotoFormBuilderAsync("/forms/new");
        await builder.SetTitleAsync(title);
        await builder.AddQuestionAsync("Text area", "Q");
        await WaitForSavedAsync();
        var id = await GetFormIdByTitleAsync(title);

        var filler = await Context.NewPageAsync();
        var response = await filler.GotoAsync(new Uri(Fixture.FormFillerBaseUrl!, $"/forms/{id}").ToString());

        // A draft-only form is not fillable.
        response.Should().NotBeNull();
        response!.Status.Should().Be((int)HttpStatusCode.NotFound);
    }

    [Fact(Skip = Blocker)]
    [Trait("Category", "Future")]
    public async Task TestSubmit_When_MessageDeliveredTwice_Should_PersistExactlyOneSubmission()
    {
        // The inbox table must make a redelivered message a no-op. Publish the same message
        // id twice onto the queue and assert a single persisted row.
        var id = await BuildAndPublishSimpleFormAsync();
        var messageId = Guid.NewGuid();

        await PublishSubmissionMessageAsync(id, messageId);
        await PublishSubmissionMessageAsync(id, messageId);

        await Task.Delay(TimeSpan.FromSeconds(2));
        var submissions = await GetSubmissionsAsync(id);
        submissions.Should().ContainSingle();
    }

    [Fact(Skip = Blocker)]
    [Trait("Category", "Future")]
    public async Task TestSubmit_When_HubConsumerBrieflyDown_Should_PersistAfterRedelivery()
    {
        var id = await BuildAndPublishSimpleFormAsync();

        // Intent: stop the consumer, submit, restart it, and assert the message is
        // redelivered and persisted, not lost. Needs a test hook to pause the consumer.
        await PublishSubmissionMessageAsync(id, Guid.NewGuid());
        await Task.Delay(TimeSpan.FromSeconds(5));

        var submissions = await GetSubmissionsAsync(id);
        submissions.Should().ContainSingle();
    }

    // ---- helpers for the future submission API (shapes TBD) --------------

    private async Task<Guid> BuildAndPublishSimpleFormAsync()
    {
        var builder = new FormBuilderPage(Page);
        var title = $"Submit-simple {Guid.NewGuid():N}";
        await GotoFormBuilderAsync("/forms/new");
        await builder.SetTitleAsync(title);
        await builder.AddQuestionAsync("Text area", "Q");
        await WaitForSavedAsync();
        await ClickPublishAsync();
        return await GetFormIdByTitleAsync(title);
    }

    private async Task<JsonElement> PollForFirstSubmissionAsync(Guid formId, int attempts = 20)
    {
        for (var i = 0; i < attempts; i++)
        {
            var submissions = await GetSubmissionsAsync(formId);
            if (submissions.Count > 0)
            {
                return submissions[0];
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        throw new TimeoutException($"No submission for form {formId} arrived at the hub.");
    }

    private async Task<List<JsonElement>> GetSubmissionsAsync(Guid formId)
    {
        using var client = Fixture.CreateHubClient();
        var response = await client.GetAsync($"/forms/{formId}/submissions");
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        return doc.ValueKind == JsonValueKind.Array ? doc.EnumerateArray().ToList() : [];
    }

    private async Task PublishSubmissionMessageAsync(Guid formId, Guid messageId)
    {
        // Placeholder for driving the queue directly or a future test endpoint. An HTTP
        // call so the test compiles. The real transport is RabbitMQ.
        using var client = Fixture.CreateHubClient();
        await client.PostAsJsonAsync($"/forms/{formId}/submissions", new { messageId, answers = Array.Empty<object>() });
    }
}

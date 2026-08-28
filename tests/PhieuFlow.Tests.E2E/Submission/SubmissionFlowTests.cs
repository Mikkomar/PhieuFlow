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
/// The ADR 0006 primary scenario and its variants: a form is built and published in one
/// browser context, filled and submitted in another, and the hub is asserted to have
/// persisted the submission across the async RabbitMQ boundary (ADR 0001).
///
/// Every test here is skipped until the form-filler service and the submission consumer
/// exist. The bodies are written against the intended contract so un-skipping is the only
/// change needed; endpoint shapes with no DTO yet are read as <see cref="JsonElement"/>.
/// </summary>
public sealed class SubmissionFlowTests(AppHostFixture fixture, ITestOutputHelper output)
    : E2ETestBase(fixture, output)
{
    private const string Blocker =
        "form-filler service + async RabbitMQ submission boundary not implemented — ADR 0001/0006";

    [Fact(Skip = Blocker)]
    [Trait("Category", "Future")]
    public async Task TestSubmit_When_AllQuestionTypesAnswered_Should_PersistSubmissionToHub()
    {
        // Context A — build + publish a form covering every question type.
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

        // Context B — the respondent fills and submits.
        var filler = await Context.NewPageAsync();
        await filler.GotoAsync(new Uri(Fixture.FormFillerBaseUrl!, $"/f/{id}").ToString());
        await filler.GetByLabel("Free text").FillAsync("Some prose");
        await filler.GetByLabel("A number").FillAsync("42");
        await filler.GetByRole(AriaRole.Radio, new() { Name = "Alpha" }).CheckAsync();
        await filler.GetByRole(AriaRole.Button, new() { Name = "Submit" }).ClickAsync();
        await Assertions.Expect(filler.GetByText("received")).ToBeVisibleAsync();

        // Hub — the submission arrives after the RabbitMQ round-trip.
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
        await filler.GotoAsync(new Uri(Fixture.FormFillerBaseUrl!, $"/f/{id}").ToString());
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
        var response = await filler.GotoAsync(new Uri(Fixture.FormFillerBaseUrl!, $"/f/{id}").ToString());

        // A draft-only form is not fillable.
        response.Should().NotBeNull();
        response!.Status.Should().Be((int)HttpStatusCode.NotFound);
    }

    [Fact(Skip = Blocker)]
    [Trait("Category", "Future")]
    public async Task TestSubmit_When_MessageDeliveredTwice_Should_PersistExactlyOneSubmission()
    {
        // Once the submission publish path exists, the inbox table (ADR 0001) must make a
        // redelivered message a no-op. Simulated by publishing the same message id twice
        // onto the submissions queue and asserting a single persisted row.
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

        // Intent: stop the hub's submission consumer, submit, restart it, and assert the
        // message is redelivered and persisted rather than lost (nack / delivery-limit,
        // ADR 0001). Requires a test hook to pause the consumer.
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
        // Placeholder for driving the submissions queue directly (or a future
        // POST /forms/{id}/submissions test endpoint). Kept as an HTTP call so the test
        // compiles; the real transport is RabbitMQ.
        using var client = Fixture.CreateHubClient();
        await client.PostAsJsonAsync($"/forms/{formId}/submissions", new { messageId, answers = Array.Empty<object>() });
    }
}

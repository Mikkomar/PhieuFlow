using System.Net.Http.Json;
using AwesomeAssertions;
using Microsoft.Playwright;
using PhieuFlow.Hub.Contracts;
using PhieuFlow.Tests.E2E.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace PhieuFlow.Tests.E2E.Versioning;

/// <summary>
/// End-to-end coverage of form versioning (ADR 0007): a published version is locked, and
/// the next edit forks a new draft rather than mutating published history.
/// </summary>
public sealed class FormVersioningFlowTests(AppHostFixture fixture, ITestOutputHelper output)
    : E2ETestBase(fixture, output)
{
    [Fact]
    public async Task TestPublish_When_DraftIsPublished_Should_MarkVersionPublishedAndLockUi()
    {
        var builder = new FormBuilderPage(Page);
        var title = $"Publish {Guid.NewGuid():N}";

        await GotoFormBuilderAsync("/forms/new");
        await builder.SetTitleAsync(title);
        await builder.AddQuestionAsync("Text area", "Anything");
        await WaitForSavedAsync();
        await ClickPublishAsync();

        var form = await GetFormAsync(await GetFormIdByTitleAsync(title));
        form.Status.Should().Be(FormVersionStatusDto.Published);
        form.VersionNumber.Should().Be(1);

        await Assertions.Expect(builder.PublishedNotice).ToBeVisibleAsync();
        await Assertions.Expect(builder.PublishButton).ToBeDisabledAsync();
    }

    [Fact]
    public async Task TestAutosave_When_EditingAfterPublish_Should_ForkNewDraftVersion()
    {
        var builder = new FormBuilderPage(Page);
        var title = $"Fork {Guid.NewGuid():N}";

        await GotoFormBuilderAsync("/forms/new");
        await builder.SetTitleAsync(title);
        await builder.AddQuestionAsync("Text area", "Original");
        await WaitForSavedAsync();
        await ClickPublishAsync();
        var id = await GetFormIdByTitleAsync(title);

        await builder.SetTitleAsync($"{title} (edited)");
        await WaitForSavedAsync();

        var form = await GetFormAsync(id);
        form.VersionNumber.Should().Be(2);
        form.Status.Should().Be(FormVersionStatusDto.Draft);

        var listItem = await GetFormListItemAsync(id);
        listItem.Should().NotBeNull();
        listItem!.LatestPublishedVersionNumber.Should().Be(1);
        listItem.VersionNumber.Should().Be(2);
    }

    [Fact]
    public async Task TestAutosave_When_EditingUnpublishedDraftRepeatedly_Should_StayOnVersionOne()
    {
        var builder = new FormBuilderPage(Page);
        var title = $"Draft-edits {Guid.NewGuid():N}";

        await GotoFormBuilderAsync("/forms/new");
        await builder.SetTitleAsync(title);
        await WaitForSavedAsync();
        var id = await GetFormIdByTitleAsync(title);

        var revisions = new List<int>();
        foreach (var description in new[] { "first", "second", "third" })
        {
            await builder.SetDescriptionAsync(description);
            await WaitForSavedAsync();
            var form = await GetFormAsync(id);
            form.VersionNumber.Should().Be(1);
            form.Status.Should().Be(FormVersionStatusDto.Draft);
            revisions.Add(form.Revision);
        }

        revisions.Should().BeInAscendingOrder();
        revisions[^1].Should().BeGreaterThan(revisions[0]);
    }

    [Fact]
    public async Task TestPublish_When_ForkedDraftIsPublished_Should_AdvanceLivePointer()
    {
        var builder = new FormBuilderPage(Page);
        var title = $"Republish {Guid.NewGuid():N}";

        await GotoFormBuilderAsync("/forms/new");
        await builder.SetTitleAsync(title);
        await builder.AddQuestionAsync("Text area", "v1 question");
        await WaitForSavedAsync();
        await ClickPublishAsync();
        var id = await GetFormIdByTitleAsync(title);

        await builder.SetDescriptionAsync("second revision");
        await WaitForSavedAsync();
        await ClickPublishAsync();

        var listItem = await GetFormListItemAsync(id);
        listItem.Should().NotBeNull();
        listItem!.LatestPublishedVersionNumber.Should().Be(2);
        listItem.VersionNumber.Should().Be(2);
        listItem.Status.Should().Be(FormVersionStatusDto.Published);
    }

    [Fact(Skip = "no GET /forms/{id}/versions/{n} endpoint to read a historical version's tree — ADR 0007")]
    [Trait("Category", "Future")]
    public async Task TestFormVersion_When_DraftForkedFromPublished_Should_LeavePublishedTreeUnchanged()
    {
        var builder = new FormBuilderPage(Page);
        var title = $"Immutable {Guid.NewGuid():N}";

        await GotoFormBuilderAsync("/forms/new");
        await builder.SetTitleAsync(title);
        await builder.AddQuestionAsync("Text area", "Question A");
        await builder.AddQuestionAsync("Text area", "Question B");
        await WaitForSavedAsync();
        await ClickPublishAsync();
        var id = await GetFormIdByTitleAsync(title);

        // Fork v2 and delete a question from it.
        await builder.DeleteQuestionAsync("Question B");
        await WaitForSavedAsync();

        using var client = Fixture.CreateHubClient();
        var v1 = await client.GetFromJsonAsync<FormDto>($"/forms/{id}/versions/1");
        v1.Should().NotBeNull();
        v1!.Pages[0].Questions.Select(q => q.Text).Should().Equal("Question A", "Question B");
    }

    [Fact]
    public async Task TestSaveAsync_When_SecondEditUsesStaleRevision_Should_RejectWithConflict()
    {
        var builder = new FormBuilderPage(Page);
        var title = $"Concurrent {Guid.NewGuid():N}";

        await GotoFormBuilderAsync("/forms/new");
        await builder.SetTitleAsync(title);
        await WaitForSavedAsync();
        var id = await GetFormIdByTitleAsync(title);

        // Two editors open the same draft.
        var editorA = await Context.NewPageAsync();
        await NavigateAsync(editorA, FormBuilderUrl($"/forms/{id}"));
        var editorB = await Context.NewPageAsync();
        await NavigateAsync(editorB, FormBuilderUrl($"/forms/{id}"));

        await new FormBuilderPage(editorA).SetDescriptionAsync("edit from A");
        await editorA.GetByText("saved just now").WaitForAsync();

        await new FormBuilderPage(editorB).SetDescriptionAsync("edit from B on a stale revision");

        // Expected once concurrency is surfaced: B's save is refused, not silently applied.
        await Assertions.Expect(editorB.GetByText("couldn't save", new() { Exact = false }))
            .ToBeVisibleAsync();
    }
}

using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using PhieuFlow.Hub.Contracts;
using PhieuFlow.Tests.Integration.Infrastructure;
using Xunit;

namespace PhieuFlow.Tests.Integration;

/// <summary>
/// <c>GET /forms/published</c> — the respondent-facing, form-filler batched list. Reuses
/// the same cursor-pagination shape as <c>GET /forms</c>
/// (<see cref="Persistence.Repositories.FormRepository.GetBatchAsync"/>), but only surfaces
/// forms with a published version, and always the published version's own content — never
/// a newer draft's.
/// </summary>
public sealed class FormPublishedListTests(HubAuthWebApplicationFactory factory)
    : IClassFixture<HubAuthWebApplicationFactory>
{
    private HttpClient WriteClient =>
        factory.CreateClientWithToken(TestJwt.Create(scope: "forms:read forms:write"));

    private HttpClient PublishedReadClient =>
        factory.CreateClientWithToken(TestJwt.Create(scope: "published-forms:read"));

    [Fact]
    public async Task TestGetFormsPublished_When_FormHasNeverBeenPublished_Should_ExcludeIt()
    {
        using var writer = WriteClient;
        await CreateDraftAsync(writer, "Never published");

        using var reader = PublishedReadClient;
        var response = await reader.GetFromJsonAsync<PublishedFormBatchResponse>("/forms/published?take=100");

        response!.Items.Should().NotContain(i => i.Title == "Never published");
    }

    [Fact]
    public async Task TestGetFormsPublished_When_FormIsPublished_Should_ReturnItWithPublishedContent()
    {
        using var writer = WriteClient;
        var id = await CreateAndPublishAsync(writer, "Contact form");

        using var reader = PublishedReadClient;
        var response = await reader.GetFromJsonAsync<PublishedFormBatchResponse>("/forms/published?take=100");

        var item = response!.Items.Should().ContainSingle(i => i.Id == id).Which;
        item.Title.Should().Be("Contact form");
        item.VersionNumber.Should().Be(1);
        item.PublishedAt.Should().NotBeNull();
        item.PageCount.Should().Be(1);
    }

    [Fact]
    public async Task TestGetFormsPublished_When_DraftEditsFollowPublish_Should_StillReturnPublishedTitleNotDraft()
    {
        using var writer = WriteClient;
        var id = await CreateAndPublishAsync(writer, "Original title");

        // Edit after publish — this forks a new draft (v2) that is never published.
        var current = await writer.GetFromJsonAsync<FormDto>($"/forms/{id}");
        current!.Title = "Unpublished edit";
        await writer.PutAsJsonAsync($"/forms/{id}", current);

        using var reader = PublishedReadClient;
        var response = await reader.GetFromJsonAsync<PublishedFormBatchResponse>("/forms/published?take=100");

        var item = response!.Items.Should().ContainSingle(i => i.Id == id).Which;
        item.Title.Should().Be("Original title");
    }

    [Fact]
    public async Task TestGetFormsPublished_When_MoreItemsExistThanTake_Should_ReturnNextStartIdForPagination()
    {
        using var writer = WriteClient;
        var firstId = await CreateAndPublishAsync(writer, "Batch A");
        var secondId = await CreateAndPublishAsync(writer, "Batch B");

        // The fixture's database is shared across tests in this class, so other tests' forms
        // may sit anywhere in Guid order relative to these two. Page all the way through with
        // take=1 (forcing at least one continuation) and assert both of *this* test's forms
        // turn up somewhere in the full traversal — that's what proves the cursor works,
        // independent of exactly how many other rows exist.
        using var reader = PublishedReadClient;
        var collected = new List<Guid>();
        Guid? startId = null;
        var sawContinuation = false;

        do
        {
            var url = startId is null ? "/forms/published?take=1" : $"/forms/published?take=1&startId={startId}";
            var page = await reader.GetFromJsonAsync<PublishedFormBatchResponse>(url);
            page!.Items.Should().ContainSingle();
            collected.AddRange(page.Items.Select(i => i.Id));

            if (startId is not null)
            {
                sawContinuation = true;
            }

            startId = page.NextStartId;
        } while (startId is not null);

        sawContinuation.Should().BeTrue("with two forms present, take=1 must require at least one continuation");
        collected.Should().Contain([firstId, secondId]);
    }

    [Fact]
    public async Task TestGetFormsPublished_When_TakeIsOutOfRange_Should_Return400()
    {
        using var reader = PublishedReadClient;

        var response = await reader.GetAsync("/forms/published?take=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task<Guid> CreateDraftAsync(HttpClient client, string title)
    {
        var created = await (await client.PostAsync("/forms", null)).Content.ReadFromJsonAsync<FormCreatedDto>();
        var id = created!.Id;

        await client.PutAsJsonAsync($"/forms/{id}", new FormDto
        {
            Id = id,
            Title = title,
            CreatedAt = DateTimeOffset.UtcNow,
            LastModifiedAt = DateTimeOffset.UtcNow,
            Revision = 1,
            VersionNumber = 1,
            Status = FormVersionStatusDto.Draft,
            Pages = [new FormPageDto { Id = Guid.NewGuid(), Title = "Page 1", Questions = [Question("Answer me")] }],
        });

        return id;
    }

    private static async Task<Guid> CreateAndPublishAsync(HttpClient client, string title)
    {
        var id = await CreateDraftAsync(client, title);
        var publish = await client.PostAsync($"/forms/{id}/publish", content: null);
        publish.EnsureSuccessStatusCode();
        return id;
    }

    private static QuestionDto Question(string text) => new TextAreaQuestionDto
    {
        Id = Guid.NewGuid(),
        Text = text,
        IsRequired = false,
    };
}

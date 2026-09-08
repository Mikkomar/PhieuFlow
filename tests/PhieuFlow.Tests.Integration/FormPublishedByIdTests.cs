using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using PhieuFlow.Hub.Contracts.Forms;
using PhieuFlow.Hub.Contracts.Publishing;
using PhieuFlow.Tests.Integration.Infrastructure;
using Xunit;

namespace PhieuFlow.Tests.Integration;

/// <summary>
/// <c>GET /forms/published/{id}</c>, the respondent-facing single-form fetch. A nonexistent
/// form and a never-published form both 404, so a bad link cannot reveal which one it is.
/// </summary>
public sealed class FormPublishedByIdTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient WriteClient => CreateClient();

    private HttpClient PublishedReadClient => CreateClient();

    [Fact]
    public async Task TestGetFormPublishedById_When_FormIsPublished_Should_ReturnFullTree()
    {
        using var writer = WriteClient;
        var id = await CreateAndPublishAsync(writer, "Contact form", "Reach out to us");

        using var reader = PublishedReadClient;
        var response = await reader.GetAsync($"/forms/published/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<PublishedFormDto>();
        dto!.Id.Should().Be(id);
        dto.VersionNumber.Should().Be(1);
        dto.Title.Should().Be("Contact form");
        dto.Description.Should().Be("Reach out to us");
        dto.Pages.Should().ContainSingle().Which.Questions.Should().ContainSingle()
            .Which.Text.Should().Be("Answer me");
    }

    [Fact]
    public async Task TestGetFormPublishedById_When_FormHasOnlyADraft_Should_Return404()
    {
        using var writer = WriteClient;
        var id = await CreateDraftAsync(writer, "Draft only");

        using var reader = PublishedReadClient;
        var response = await reader.GetAsync($"/forms/published/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TestGetFormPublishedById_When_FormDoesNotExist_Should_Return404()
    {
        using var reader = PublishedReadClient;

        var response = await reader.GetAsync($"/forms/published/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TestGetFormPublishedById_When_DraftEditsFollowPublish_Should_StillReturnPublishedContent()
    {
        using var writer = WriteClient;
        var id = await CreateAndPublishAsync(writer, "Original title", description: null);

        var current = await writer.GetFromJsonAsync<FormDto>($"/forms/{id}");
        current!.Title = "Unpublished edit";
        await writer.PutAsJsonAsync($"/forms/{id}", current);

        using var reader = PublishedReadClient;
        var dto = await reader.GetFromJsonAsync<PublishedFormDto>($"/forms/published/{id}");

        dto!.Title.Should().Be("Original title");
    }

    private static async Task<Guid> CreateDraftAsync(HttpClient client, string title, string? description = null)
    {
        var created = await (await client.PostAsync("/forms", null)).Content.ReadFromJsonAsync<FormCreatedDto>();
        var id = created!.Id;

        await client.PutAsJsonAsync($"/forms/{id}", new FormDto
        {
            Id = id,
            Title = title,
            Description = description,
            CreatedAt = DateTimeOffset.UtcNow,
            LastModifiedAt = DateTimeOffset.UtcNow,
            Revision = 1,
            VersionNumber = 1,
            Status = FormVersionStatusDto.Draft,
            Pages = [new FormPageDto { Id = Guid.NewGuid(), Title = "Page 1", Questions = [Question("Answer me")] }],
        });

        return id;
    }

    private static async Task<Guid> CreateAndPublishAsync(HttpClient client, string title, string? description)
    {
        var id = await CreateDraftAsync(client, title, description);
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

using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using PhieuFlow.Hub.Contracts.Forms;
using PhieuFlow.Tests.Integration.Infrastructure;
using Xunit;

namespace PhieuFlow.Tests.Integration;

/// <summary>
/// <c>GET /forms/{id}/versions/{n}</c>, the builder-facing read of one version's tree by
/// number. It returns any status: a published version stays readable, frozen, after a later
/// edit forks a new draft, and the current draft is readable by its own number. A missing
/// version number or an unknown form both 404.
/// </summary>
public sealed class FormVersionByNumberTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Client => CreateClient();

    [Fact]
    public async Task TestGetFormVersion_When_VersionIsAPublishedHistoricalVersion_Should_ReturnFrozenTree()
    {
        using var client = Client;
        var id = await CreateAndPublishTwoQuestionFormAsync(client, "Survey");
        await ForkByDeletingLastQuestionAsync(client, id);

        var v1 = await client.GetFromJsonAsync<FormDto>($"/forms/{id}/versions/1");

        v1!.VersionNumber.Should().Be(1);
        v1.Status.Should().Be(FormVersionStatusDto.Published);
        v1.Pages[0].Questions.Select(q => q.Text).Should().Equal("Question A", "Question B");
    }

    [Fact]
    public async Task TestGetFormVersion_When_VersionIsTheCurrentDraft_Should_ReturnDraftTree()
    {
        using var client = Client;
        var id = await CreateAndPublishTwoQuestionFormAsync(client, "Survey");
        await ForkByDeletingLastQuestionAsync(client, id);

        var v2 = await client.GetFromJsonAsync<FormDto>($"/forms/{id}/versions/2");

        v2!.VersionNumber.Should().Be(2);
        v2.Status.Should().Be(FormVersionStatusDto.Draft);
        v2.Pages[0].Questions.Select(q => q.Text).Should().Equal("Question A");
    }

    [Fact]
    public async Task TestGetFormVersion_When_VersionNumberDoesNotExist_Should_Return404()
    {
        using var client = Client;
        var id = await CreateAndPublishTwoQuestionFormAsync(client, "Survey");

        var response = await client.GetAsync($"/forms/{id}/versions/99");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TestGetFormVersion_When_FormDoesNotExist_Should_Return404()
    {
        using var client = Client;

        var response = await client.GetAsync($"/forms/{Guid.NewGuid()}/versions/1");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<Guid> CreateAndPublishTwoQuestionFormAsync(HttpClient client, string title)
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
            Pages =
            [
                new FormPageDto
                {
                    Id = Guid.NewGuid(),
                    Title = "Page 1",
                    Questions = [Question("Question A"), Question("Question B")],
                },
            ],
        });

        (await client.PostAsync($"/forms/{id}/publish", content: null)).EnsureSuccessStatusCode();
        return id;
    }

    // Editing the published version forks a fresh draft (v2). Drop its last question.
    private static async Task ForkByDeletingLastQuestionAsync(HttpClient client, Guid id)
    {
        var current = await client.GetFromJsonAsync<FormDto>($"/forms/{id}");
        current!.Pages[0].Questions = current.Pages[0].Questions.SkipLast(1).ToList();

        var response = await client.PutAsJsonAsync($"/forms/{id}", current);
        response.EnsureSuccessStatusCode();
        (await response.Content.ReadFromJsonAsync<FormVersionStateDto>())!.VersionNumber.Should().Be(2);
    }

    private static QuestionDto Question(string text) => new TextAreaQuestionDto
    {
        Id = Guid.NewGuid(),
        Text = text,
        IsRequired = false,
    };
}

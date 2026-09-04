using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using PhieuFlow.Hub.Contracts;
using PhieuFlow.Tests.Integration.Infrastructure;
using Xunit;

namespace PhieuFlow.Tests.Integration;

/// <summary>
/// <c>PUT /forms/{id}</c> updates the latest version of an existing form and needs
/// <c>forms:write</c>. It never creates: a PUT to an id the Hub does not know 404s rather
/// than resurrecting it from a client-supplied primary key (creation is <c>POST /forms</c>
/// only, and that endpoint mints the id server-side). It also enforces optimistic concurrency:
/// a PUT whose <c>VersionNumber</c>/<c>Revision</c> no longer matches the persisted latest row
/// 409s and writes nothing, so a second editor can't silently overwrite the first.
/// </summary>
public sealed class FormSaveTests(HubAuthWebApplicationFactory factory)
    : IClassFixture<HubAuthWebApplicationFactory>
{
    private HttpClient WriteClient =>
        factory.CreateClientWithToken(TestJwt.Create(scope: "forms:read forms:write"));

    [Fact]
    public async Task TestCreate_Should_MintTheIdServerSide()
    {
        using var client = WriteClient;

        var response = await client.PostAsync("/forms", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await response.Content.ReadFromJsonAsync<FormCreatedDto>();
        created!.Id.Should().NotBeEmpty();
        (await client.GetAsync($"/forms/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TestSaveAsync_When_FormDoesNotExist_Should_Return404()
    {
        using var client = WriteClient;
        var strayId = Guid.NewGuid();

        var response = await client.PutAsJsonAsync($"/forms/{strayId}", FormDtoFor(strayId, "Ghost"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync($"/forms/{strayId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TestSaveAsync_When_FormExists_Should_PersistEditsAndReturnState()
    {
        using var client = WriteClient;
        var id = await CreateFormAsync(client);

        var response = await client.PutAsJsonAsync($"/forms/{id}", FormDtoFor(id, "Renamed"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var state = await response.Content.ReadFromJsonAsync<FormVersionStateDto>();
        state.Should().NotBeNull();

        var reread = await client.GetFromJsonAsync<FormDto>($"/forms/{id}");
        reread!.Title.Should().Be("Renamed");
    }

    [Fact]
    public async Task TestSaveAsync_When_RevisionMatches_Should_BumpRevisionEachSave()
    {
        using var client = WriteClient;
        var id = await CreateFormAsync(client);

        var first = await PutAsync(client, id, "First", revision: 1, versionNumber: 1);
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        (await first.Content.ReadFromJsonAsync<FormVersionStateDto>())!.Revision.Should().Be(2);

        var second = await PutAsync(client, id, "Second", revision: 2, versionNumber: 1);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Content.ReadFromJsonAsync<FormVersionStateDto>())!.Revision.Should().Be(3);
    }

    [Fact]
    public async Task TestSaveAsync_When_RevisionIsStale_Should_Return409()
    {
        using var client = WriteClient;
        var id = await CreateFormAsync(client);

        // A first save takes the server to Revision 2.
        var fresh = await PutAsync(client, id, "Editor A", revision: 1, versionNumber: 1);
        fresh.StatusCode.Should().Be(HttpStatusCode.OK);

        // A second editor still on Revision 1 tries to save.
        var stale = await PutAsync(client, id, "Editor B (stale)", revision: 1, versionNumber: 1);

        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var reread = await client.GetFromJsonAsync<FormDto>($"/forms/{id}");
        reread!.Title.Should().Be("Editor A");
    }

    [Fact]
    public async Task TestSaveAsync_When_VersionForkedButStaleRevisionCollides_Should_Return409()
    {
        using var client = WriteClient;
        var id = await CreateFormAsync(client);

        // v1: r1 -> r2.
        (await PutAsync(client, id, "v1 edit", revision: 1, versionNumber: 1))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.PostAsync($"/forms/{id}/publish", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Editing the published v1 (client knows v1/r2) forks v2, which starts at r1.
        var fork = await PutAsync(client, id, "v2 edit", revision: 2, versionNumber: 1);
        fork.StatusCode.Should().Be(HttpStatusCode.OK);
        var forked = await fork.Content.ReadFromJsonAsync<FormVersionStateDto>();
        forked!.VersionNumber.Should().Be(2);
        forked.Revision.Should().Be(1);

        // Advance v2 to r2 so its Revision now equals the stale v1 client's Revision.
        (await PutAsync(client, id, "v2 edit 2", revision: 1, versionNumber: 2))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // The stale v1 client (still VersionNumber 1, Revision 2) must not overwrite v2 even
        // though Revision 2 collides with the live v2 row.
        var stale = await PutAsync(client, id, "v1 clobber", revision: 2, versionNumber: 1);

        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var reread = await client.GetFromJsonAsync<FormDto>($"/forms/{id}");
        reread!.VersionNumber.Should().Be(2);
        reread.Title.Should().Be("v2 edit 2");
    }

    private static async Task<Guid> CreateFormAsync(HttpClient client)
    {
        var created = await (await client.PostAsync("/forms", null)).Content.ReadFromJsonAsync<FormCreatedDto>();
        return created!.Id;
    }

    private static Task<HttpResponseMessage> PutAsync(
        HttpClient client, Guid id, string title, int revision, int versionNumber) =>
        client.PutAsJsonAsync($"/forms/{id}", FormDtoFor(id, title, revision, versionNumber));

    private static FormDto FormDtoFor(Guid id, string title, int revision = 1, int versionNumber = 1) => new()
    {
        Id = id,
        Title = title,
        CreatedAt = DateTimeOffset.UtcNow,
        LastModifiedAt = DateTimeOffset.UtcNow,
        Revision = revision,
        VersionNumber = versionNumber,
        Status = FormVersionStatusDto.Draft,
        Pages =
        [
            new FormPageDto
            {
                Id = Guid.NewGuid(),
                Title = "Page 1",
                Questions = [new TextAreaQuestionDto { Id = Guid.NewGuid(), Text = "Answer me", IsRequired = false }],
            },
        ],
    };
}

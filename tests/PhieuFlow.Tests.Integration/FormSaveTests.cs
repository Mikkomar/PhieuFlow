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
/// than resurrecting it from a client-supplied primary key. Creation is <c>POST /forms</c>
/// only, and that endpoint mints the id server-side.
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
        var created = await (await client.PostAsync("/forms", null)).Content.ReadFromJsonAsync<FormCreatedDto>();
        var id = created!.Id;

        var response = await client.PutAsJsonAsync($"/forms/{id}", FormDtoFor(id, "Renamed"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var state = await response.Content.ReadFromJsonAsync<FormVersionStateDto>();
        state.Should().NotBeNull();

        var reread = await client.GetFromJsonAsync<FormDto>($"/forms/{id}");
        reread!.Title.Should().Be("Renamed");
    }

    private static FormDto FormDtoFor(Guid id, string title) => new()
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
                Questions = [new TextAreaQuestionDto { Id = Guid.NewGuid(), Text = "Answer me", IsRequired = false }],
            },
        ],
    };
}

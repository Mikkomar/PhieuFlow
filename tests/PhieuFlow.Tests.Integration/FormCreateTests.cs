using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using PhieuFlow.Hub.Contracts;
using PhieuFlow.Tests.Integration.Infrastructure;
using Xunit;

namespace PhieuFlow.Tests.Integration;

/// <summary>
/// <c>POST /forms</c> mints a blank draft so the builder can open it by id (and a reload
/// re-loads it). The id comes from the Hub, not the client.
/// </summary>
public sealed class FormCreateTests(HubAuthWebApplicationFactory factory)
    : IClassFixture<HubAuthWebApplicationFactory>
{
    private HttpClient WriteClient =>
        factory.CreateClientWithToken(TestJwt.Create(scope: "forms:read forms:write"));

    [Fact]
    public async Task TestCreate_Should_PersistABlankDraftRetrievableById()
    {
        using var client = WriteClient;

        var response = await client.PostAsync("/forms", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await response.Content.ReadFromJsonAsync<FormCreatedDto>();
        created!.Id.Should().NotBe(Guid.Empty);

        var fetched = await client.GetFromJsonAsync<FormDto>($"/forms/{created.Id}");
        fetched!.Id.Should().Be(created.Id);
        fetched.Title.Should().BeEmpty();
        fetched.Status.Should().Be(FormVersionStatusDto.Draft);
        fetched.VersionNumber.Should().Be(1);
        fetched.Pages.Should().ContainSingle().Which.Questions.Should().BeEmpty();
    }

    [Fact]
    public async Task TestCreate_When_CalledTwice_Should_MintDistinctForms()
    {
        using var client = WriteClient;

        var first = await (await client.PostAsync("/forms", null)).Content.ReadFromJsonAsync<FormCreatedDto>();
        var second = await (await client.PostAsync("/forms", null)).Content.ReadFromJsonAsync<FormCreatedDto>();

        first!.Id.Should().NotBe(second!.Id);
    }
}

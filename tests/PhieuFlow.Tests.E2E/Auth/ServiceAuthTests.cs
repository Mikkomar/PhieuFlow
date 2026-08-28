using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AwesomeAssertions;
using PhieuFlow.Hub.Contracts;
using PhieuFlow.Tests.E2E.Infrastructure;
using Xunit;

namespace PhieuFlow.Tests.E2E.Auth;

/// <summary>
/// ADR 0005: the hub authenticates its callers with OAuth2 client-credentials tokens
/// (Keycloak) and authorises by scope claim (<c>forms:read</c>, <c>forms:write</c>,
/// <c>submissions:write</c>). These drive the hub REST API directly — no browser.
/// </summary>
[Collection(E2ECollection.Name)]
public sealed class ServiceAuthTests(AppHostFixture fixture)
{
    [Fact]
    public async Task TestGetForms_Without_BearerToken_Should_Return401()
    {
        using var client = fixture.CreateHubClient();

        var response = await client.GetAsync("/forms");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TestPutForm_When_TokenHasReadOnlyScope_Should_Return403()
    {
        using var client = fixture.CreateHubClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("forms:read"));

        var read = await client.GetAsync("/forms");
        read.StatusCode.Should().Be(HttpStatusCode.OK);

        var write = await client.PutAsJsonAsync($"/forms/{Guid.NewGuid()}", NewFormDto());
        write.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TestHubApi_When_TokenHasBuilderClientCredentials_Should_AllowReadAndWrite()
    {
        using var client = fixture.CreateHubClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetTokenAsync("forms:read", "forms:write"));

        var id = Guid.NewGuid();
        var write = await client.PutAsJsonAsync($"/forms/{id}", NewFormDto());
        write.StatusCode.Should().Be(HttpStatusCode.OK);

        var read = await client.GetAsync($"/forms/{id}");
        read.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private Task<string> GetTokenAsync(params string[] scopes) => fixture.GetServiceTokenAsync(scopes);

    private static FormDto NewFormDto() => new()
    {
        Id = Guid.NewGuid(),
        Title = "Auth probe",
        CreatedAt = DateTimeOffset.UtcNow,
        LastModifiedAt = DateTimeOffset.UtcNow,
        Revision = 1,
        VersionNumber = 1,
        Status = FormVersionStatusDto.Draft,
        Pages = [],
    };
}

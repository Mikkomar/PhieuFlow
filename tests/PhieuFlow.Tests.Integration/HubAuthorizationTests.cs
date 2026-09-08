using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using PhieuFlow.Hub.Contracts.Forms;
using PhieuFlow.Tests.Integration.Infrastructure;
using Xunit;

namespace PhieuFlow.Tests.Integration;

/// <summary>
/// ADR 0005: the Hub rejects unauthenticated callers (401) and callers whose token
/// lacks the required scope (403), and serves callers whose token carries it. The
/// integration-auth tier — runs the real Hub in-process with an offline-validated test
/// token and in-memory SQLite, no Keycloak, no container. Authorization runs before the
/// endpoint delegate, so the write-scope gates below 403 without any row having to exist.
/// </summary>
[Collection(AuthCollection.Name)]
public sealed class HubAuthorizationTests(HubAuthWebApplicationFactory factory)
{
    [Fact]
    public async Task TestGetForms_Without_BearerToken_Should_Return401()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/forms");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TestGetForms_When_BearerTokenIsGarbage_Should_Return401()
    {
        using var client = factory.CreateClientWithToken("not-a-jwt");

        var response = await client.GetAsync("/forms");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TestGetForms_When_TokenSignedWithUnknownKey_Should_Return401()
    {
        var token = TestJwt.Create(scope: "forms:read", signingKey: TestJwt.UnknownKey);
        using var client = factory.CreateClientWithToken(token);

        var response = await client.GetAsync("/forms");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TestGetForms_When_TokenAudienceIsWrong_Should_Return401()
    {
        var token = TestJwt.Create(scope: "forms:read", audience: "some-other-api");
        using var client = factory.CreateClientWithToken(token);

        var response = await client.GetAsync("/forms");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TestGetForms_When_TokenHasExpired_Should_Return401()
    {
        var token = TestJwt.Create(scope: "forms:read", expires: DateTime.UtcNow.AddMinutes(-1));
        using var client = factory.CreateClientWithToken(token);

        var response = await client.GetAsync("/forms");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TestGetForms_When_TokenHasReadScope_Should_Return200()
    {
        var token = TestJwt.Create(scope: "forms:read");
        using var client = factory.CreateClientWithToken(token);

        var response = await client.GetAsync("/forms");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TestGetForms_When_ScopesAreInScpClaim_Should_Return200()
    {
        var token = TestJwt.Create(scope: "forms:read forms:write", scopeClaimName: "scp");
        using var client = factory.CreateClientWithToken(token);

        var response = await client.GetAsync("/forms");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TestGetForms_When_TokenHasNoScopeClaim_Should_Return403()
    {
        var token = TestJwt.Create(scope: null);
        using var client = factory.CreateClientWithToken(token);

        var response = await client.GetAsync("/forms");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TestPutForm_When_TokenHasReadScopeOnly_Should_Return403()
    {
        var token = TestJwt.Create(scope: "forms:read");
        using var client = factory.CreateClientWithToken(token);

        var response = await client.PutAsJsonAsync($"/forms/{Guid.NewGuid()}", NewFormDto());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TestDeleteForm_When_TokenHasReadScopeOnly_Should_Return403()
    {
        using var client = factory.CreateClientWithToken(TestJwt.Create(scope: "forms:read"));

        var response = await client.DeleteAsync($"/forms/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TestDuplicateForm_When_TokenHasReadScopeOnly_Should_Return403()
    {
        using var client = factory.CreateClientWithToken(TestJwt.Create(scope: "forms:read"));

        var response = await client.PostAsync($"/forms/{Guid.NewGuid()}/duplicate", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TestGetFormPublishedById_When_TokenHasOnlyFormsReadScope_Should_Return403()
    {
        using var client = factory.CreateClientWithToken(TestJwt.Create(scope: "forms:read"));

        var response = await client.GetAsync($"/forms/published/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TestGetFormSubmissions_Without_BearerToken_Should_Return401()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/forms/{Guid.NewGuid()}/submissions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TestGetFormSubmissions_When_TokenHasOnlyFormsReadScope_Should_Return403()
    {
        using var client = factory.CreateClientWithToken(TestJwt.Create(scope: "forms:read"));

        var response = await client.GetAsync($"/forms/{Guid.NewGuid()}/submissions");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TestGetFormSubmissions_When_TokenHasSubmissionsReadScope_Should_PassAuthorization()
    {
        using var client = factory.CreateClientWithToken(TestJwt.Create(scope: "submissions:read"));

        var response = await client.GetAsync($"/forms/{Guid.NewGuid()}/submissions");

        // Authorization passed; the unknown form is a 404 from the endpoint, not a 403.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TestPutForm_When_TokenHasWriteScope_Should_Return200()
    {
        var token = TestJwt.Create(scope: "forms:read forms:write");
        using var client = factory.CreateClientWithToken(token);

        var created = await (await client.PostAsync("/forms", null)).Content.ReadFromJsonAsync<FormCreatedDto>();
        var id = created!.Id;

        var write = await client.PutAsJsonAsync($"/forms/{id}", NewFormDto(id));
        write.StatusCode.Should().Be(HttpStatusCode.OK);

        var read = await client.GetAsync($"/forms/{id}");
        read.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TestGetFormsPublished_Without_BearerToken_Should_Return401()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/forms/published");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TestGetFormPublishedById_Without_BearerToken_Should_Return401()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/forms/published/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TestGetFormsPublished_When_TokenHasPublishedFormsReadScope_Should_Return200()
    {
        var token = TestJwt.Create(scope: "published-forms:read");
        using var client = factory.CreateClientWithToken(token);

        var response = await client.GetAsync("/forms/published");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // Isolation (ADR 0005 extension): form-filler's token must never satisfy forms:read, and
    // form-builder's token must never satisfy published-forms:read — the two clients are
    // scoped to disjoint capabilities so form-filler structurally cannot read draft content.
    [Fact]
    public async Task TestGetForms_When_TokenHasOnlyPublishedFormsReadScope_Should_Return403()
    {
        var token = TestJwt.Create(scope: "published-forms:read");
        using var client = factory.CreateClientWithToken(token);

        var response = await client.GetAsync("/forms");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TestGetFormsPublished_When_TokenHasOnlyFormsReadScope_Should_Return403()
    {
        var token = TestJwt.Create(scope: "forms:read");
        using var client = factory.CreateClientWithToken(token);

        var response = await client.GetAsync("/forms/published");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static FormDto NewFormDto(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Title = "Auth probe",
        CreatedAt = DateTimeOffset.UtcNow,
        LastModifiedAt = DateTimeOffset.UtcNow,
        Revision = 1,
        VersionNumber = 1,
        Status = FormVersionStatusDto.Draft,
        Pages = [],
    };
}

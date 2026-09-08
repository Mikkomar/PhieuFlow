using System.Net;
using System.Text;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PhieuFlow.ServiceAuth;
using Xunit;

namespace PhieuFlow.Tests.Unit;

/// <summary>
/// Unit coverage of <see cref="ClientCredentialsTokenProvider"/> (ADR 0005) — the
/// process-wide token cache, refresh-skew expiry, and single-flight gate — with a stub
/// Keycloak and a hand-driven clock, no container.
/// </summary>
public sealed class ClientCredentialsTokenProviderTests
{
    private static readonly DateTimeOffset Origin = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static ClientCredentialsTokenProvider ProviderFor(
        StubHttpMessageHandler keycloak, TimeProvider time) =>
        new(
            new StubHttpClientFactory(keycloak),
            Options.Create(new KeycloakClientOptions
            {
                Authority = "https://keycloak.test/realms/phieuflow",
                ClientId = "form-builder",
                ClientSecret = "secret",
                Scope = "forms:write",
            }),
            time,
            NullLogger<ClientCredentialsTokenProvider>.Instance);

    [Fact]
    public async Task TestGetAsync_When_CalledAgainBeforeExpiry_Should_ReturnCachedTokenWithoutARequest()
    {
        var keycloak = new StubHttpMessageHandler().RespondWithToken("token-1", expiresInSeconds: 300);
        var time = new MutableTimeProvider(Origin);
        var provider = ProviderFor(keycloak, time);

        var first = await provider.GetAsync(CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(120));
        var second = await provider.GetAsync(CancellationToken.None);

        first.Should().Be("token-1");
        second.Should().Be("token-1");
        keycloak.SendCount.Should().Be(1);
        keycloak.Requests[0].Body.Should().Contain("grant_type=client_credentials");
    }

    [Fact]
    public async Task TestGetAsync_When_TokenHasExpired_Should_RequestAFreshToken()
    {
        var keycloak = new StubHttpMessageHandler()
            .RespondWithToken("token-1", expiresInSeconds: 300)
            .RespondWithToken("token-2", expiresInSeconds: 300);
        var time = new MutableTimeProvider(Origin);
        var provider = ProviderFor(keycloak, time);

        var first = await provider.GetAsync(CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(300)); // past expiry once the 30s refresh skew is applied
        var second = await provider.GetAsync(CancellationToken.None);

        first.Should().Be("token-1");
        second.Should().Be("token-2");
        keycloak.SendCount.Should().Be(2);
    }

    [Fact]
    public async Task TestGetAsync_When_ManyCallersRaceOnAColdCache_Should_IssueOneTokenRequest()
    {
        // A slow responder widens the window in which callers can pile up on the gate.
        var keycloak = new StubHttpMessageHandler().Respond(async (_, ct) =>
        {
            await Task.Delay(25, ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"access_token":"token-1","expires_in":300}""",
                    Encoding.UTF8,
                    "application/json"),
            };
        });
        var provider = ProviderFor(keycloak, new MutableTimeProvider(Origin));

        var tokens = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => provider.GetAsync(CancellationToken.None)));

        tokens.Should().OnlyContain(t => t == "token-1");
        keycloak.SendCount.Should().Be(1);
    }

    [Fact]
    public async Task TestGetAsync_When_KeycloakReturnsAnError_Should_Throw()
    {
        var keycloak = new StubHttpMessageHandler().RespondWithStatus(HttpStatusCode.Unauthorized);
        var provider = ProviderFor(keycloak, new MutableTimeProvider(Origin));

        var act = () => provider.GetAsync(CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task TestInvalidate_When_Called_Should_MakeTheNextGetAsyncRefetch()
    {
        var keycloak = new StubHttpMessageHandler()
            .RespondWithToken("token-1", expiresInSeconds: 300)
            .RespondWithToken("token-2", expiresInSeconds: 300);
        var provider = ProviderFor(keycloak, new MutableTimeProvider(Origin));

        var first = await provider.GetAsync(CancellationToken.None);
        provider.Invalidate();
        var second = await provider.GetAsync(CancellationToken.None);

        first.Should().Be("token-1");
        second.Should().Be("token-2");
        keycloak.SendCount.Should().Be(2);
    }
}

using System.Net;
using System.Text;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PhieuFlow.ServiceAuth;
using Xunit;

namespace PhieuFlow.Tests.Unit;

/// <summary>
/// Unit coverage of <see cref="ClientCredentialsTokenHandler"/> (ADR 0005) — bearer
/// attachment and the single invalidate-and-retry on a 401 — driven through an
/// <see cref="HttpMessageInvoker"/> with a stub Hub.
/// </summary>
public sealed class ClientCredentialsTokenHandlerTests
{
    private static (ClientCredentialsTokenHandler Handler, StubHttpMessageHandler Keycloak) HandlerFor(
        StubHttpMessageHandler hub)
    {
        var keycloak = new StubHttpMessageHandler().RespondWithToken("t", expiresInSeconds: 300);
        var provider = new ClientCredentialsTokenProvider(
            new StubHttpClientFactory(keycloak),
            Options.Create(new KeycloakClientOptions
            {
                Authority = "https://keycloak.test/realms/phieuflow",
                ClientId = "form-builder",
                ClientSecret = "secret",
                Scope = "forms:write",
            }),
            TimeProvider.System,
            NullLogger<ClientCredentialsTokenProvider>.Instance);

        var handler = new ClientCredentialsTokenHandler(provider, NullLogger<ClientCredentialsTokenHandler>.Instance)
        {
            InnerHandler = hub,
        };
        return (handler, keycloak);
    }

    [Fact]
    public async Task TestSendAsync_When_HubDoesNotReturn401_Should_AttachTheBearerAndPassTheResponseThrough()
    {
        var hub = new StubHttpMessageHandler().RespondWithStatus(HttpStatusCode.OK);
        var (handler, keycloak) = HandlerFor(hub);
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://hub.test/forms");

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        hub.SendCount.Should().Be(1);
        hub.Requests[0].Authorization.Should().Be("Bearer t");
        keycloak.SendCount.Should().Be(1);
    }

    [Fact]
    public async Task TestSendAsync_When_HubReturns401_Should_InvalidateRefetchAndRetryOnce()
    {
        var hub = new StubHttpMessageHandler()
            .RespondWithStatus(HttpStatusCode.Unauthorized)
            .RespondWithStatus(HttpStatusCode.OK);
        var (handler, keycloak) = HandlerFor(hub);
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://hub.test/forms");

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        hub.SendCount.Should().Be(2);
        keycloak.SendCount.Should().Be(2); // initial acquire + refetch after Invalidate()
    }

    [Fact]
    public async Task TestSendAsync_When_RetryingAfter401_Should_ReplayTheRequestBody()
    {
        var hub = new StubHttpMessageHandler()
            .RespondWithStatus(HttpStatusCode.Unauthorized)
            .RespondWithStatus(HttpStatusCode.OK);
        var (handler, _) = HandlerFor(hub);
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Put, "https://hub.test/forms/1")
        {
            Content = new StringContent("""{"title":"x"}""", Encoding.UTF8, "application/json"),
        };

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        hub.SendCount.Should().Be(2);
        hub.Requests[1].Body.Should().Be("""{"title":"x"}""");
        hub.Requests[1].ContentType.Should().Be("application/json; charset=utf-8");
    }

    [Fact]
    public async Task TestSendAsync_When_TheRetryAlsoReturns401_Should_ReturnThatResponseWithoutRetryingAgain()
    {
        var hub = new StubHttpMessageHandler()
            .RespondWithStatus(HttpStatusCode.Unauthorized)
            .RespondWithStatus(HttpStatusCode.Unauthorized);
        var (handler, _) = HandlerFor(hub);
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://hub.test/forms");

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        hub.SendCount.Should().Be(2);
    }
}

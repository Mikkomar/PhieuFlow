using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PhieuFlow.ServiceAuth;
using Xunit;

namespace PhieuFlow.Tests.Unit;

/// <summary>
/// Unit coverage of <c>AddKeycloakClientCredentials</c> (ADR 0005) — the per-service scope
/// default that replaced the two divergent compiled-in defaults, and the registrations it
/// contributes.
/// </summary>
public sealed class ClientCredentialsExtensionsTests
{
    [Fact]
    public void TestAddKeycloakClientCredentials_When_ConfigDoesNotSetAScope_Should_ApplyTheServiceDefault()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddKeycloakClientCredentials(defaultScope: "published-forms:read");
        using var host = builder.Build();

        host.Services.GetRequiredService<IOptions<KeycloakClientOptions>>()
            .Value.Scope.Should().Be("published-forms:read");
    }

    [Fact]
    public void TestAddKeycloakClientCredentials_When_ConfigProvidesAScope_Should_KeepTheConfiguredValue()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Keycloak:Scope"] = "custom:scope",
        });

        builder.AddKeycloakClientCredentials(defaultScope: "published-forms:read");
        using var host = builder.Build();

        host.Services.GetRequiredService<IOptions<KeycloakClientOptions>>()
            .Value.Scope.Should().Be("custom:scope");
    }

    [Fact]
    public void TestAddKeycloakClientCredentials_When_Called_Should_RegisterTheTokenProviderAndHandler()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddKeycloakClientCredentials(defaultScope: "forms:write");
        using var host = builder.Build();

        host.Services.GetService<ClientCredentialsTokenProvider>().Should().NotBeNull();
        host.Services.GetService<ClientCredentialsTokenHandler>().Should().NotBeNull();
        host.Services.GetService<IHttpClientFactory>().Should().NotBeNull();
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PhieuFlow.ServiceAuth;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Wires up the OAuth2 client-credentials flow (ADR 0005) that the form-builder and
/// form-filler use to authenticate their synchronous Hub calls. Both services share one
/// implementation in <c>PhieuFlow.ServiceAuth</c>, differing only in the default scope.
/// </summary>
public static class ClientCredentialsExtensions
{
    /// <summary>
    /// Binds <see cref="KeycloakClientOptions"/> from the <c>Keycloak</c> configuration
    /// section, falling back to <paramref name="defaultScope"/> when no scope is
    /// configured, and registers the token provider, the delegating handler, and the
    /// <c>keycloak-token</c> named client (with a development-only bypass for Aspire's
    /// self-signed certificate). Call <see cref="AddClientCredentialsToken"/> to attach
    /// the handler to the typed Hub client.
    /// </summary>
    public static IHostApplicationBuilder AddKeycloakClientCredentials(
        this IHostApplicationBuilder builder, string defaultScope)
    {
        builder.Services.Configure<KeycloakClientOptions>(
            builder.Configuration.GetSection("Keycloak"));
        builder.Services.PostConfigure<KeycloakClientOptions>(o =>
        {
            if (string.IsNullOrWhiteSpace(o.Scope))
            {
                o.Scope = defaultScope;
            }
        });

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<ClientCredentialsTokenProvider>();
        builder.Services.AddTransient<ClientCredentialsTokenHandler>();

        var tokenClient = builder.Services.AddHttpClient("keycloak-token");
        if (builder.Environment.IsDevelopment())
        {
            // Local orchestration only: Aspire serves Keycloak over a self-signed certificate.
            tokenClient.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            });
        }

        return builder;
    }

    /// <summary>
    /// Attaches the client-credentials bearer token to a typed Hub client, so every call
    /// it makes carries a valid token and retries once on a 401.
    /// </summary>
    public static IHttpClientBuilder AddClientCredentialsToken(this IHttpClientBuilder builder)
        => builder.AddHttpMessageHandler<ClientCredentialsTokenHandler>();
}

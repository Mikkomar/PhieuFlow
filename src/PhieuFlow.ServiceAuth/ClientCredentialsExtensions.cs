using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PhieuFlow.ServiceAuth;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Sets up the OAuth2 client-credentials flow the form-builder and form-filler use for
/// their synchronous Hub calls. Only the default scope differs between them.
/// </summary>
public static class ClientCredentialsExtensions
{
    /// <summary>
    /// Binds <see cref="KeycloakClientOptions"/> from the <c>Keycloak</c> section, defaults
    /// the scope to <paramref name="defaultScope"/>, and registers the token provider and handler.
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
            // Local only: Aspire serves Keycloak over a self-signed certificate.
            tokenClient.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            });
        }

        return builder;
    }

    /// <summary>
    /// Attaches the bearer token to a typed Hub client. Every call then carries a valid
    /// token, and a 401 triggers one retry.
    /// </summary>
    public static IHttpClientBuilder AddClientCredentialsToken(this IHttpClientBuilder builder)
        => builder.AddHttpMessageHandler<ClientCredentialsTokenHandler>();
}

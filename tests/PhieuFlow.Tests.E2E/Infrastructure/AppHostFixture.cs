using System.Net.Http.Headers;
using System.Net.Http.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Xunit;

namespace PhieuFlow.Tests.E2E.Infrastructure;

/// <summary>
/// The one expensive shared resource for the whole E2E assembly. Starts the full AppHost
/// graph (SQL, migrations, seed, Keycloak, hub, form-builder) via
/// <see cref="DistributedApplicationTestingBuilder"/>, then one headless Chromium.
/// </summary>
public sealed class AppHostFixture : IAsyncLifetime
{
    // Keycloak cold start (image pull, JVM boot, realm import) adds to the already-slow
    // SQL Server container.
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(10);

    // Matches the realm imported by the AppHost (src/PhieuFlow.AppHost/realms).
    private const string Realm = "phieuflow";
    private const string FormBuilderClientId = "form-builder";
    private const string FormBuilderClientSecret = "form-builder-dev-secret";
    private const string FormFillerClientId = "form-filler";
    private const string FormFillerClientSecret = "form-filler-dev-secret";

    private DistributedApplication _app = null!;
    private IPlaywright _playwright = null!;

    public IBrowser Browser { get; private set; } = null!;

    /// <summary>Base URL of the running form-builder UI (Blazor Server).</summary>
    public Uri FormBuilderBaseUrl { get; private set; } = null!;

    /// <summary>Base URL of the Keycloak identity provider.</summary>
    public Uri KeycloakBaseUrl { get; private set; } = null!;

    /// <summary>Base URL of the running form-filler UI. The submission tests navigate to it.</summary>
    public Uri FormFillerBaseUrl { get; private set; } = null!;

    /// <summary>
    /// Bare HTTP client for the hub REST API, no bearer token. Use it only to assert the
    /// hub rejects unauthenticated callers. Everything else needs
    /// <see cref="CreateAuthorizedHubClientAsync"/>.
    /// </summary>
    public HttpClient CreateHubClient() => _app.CreateHttpClient("hub");

    /// <summary>
    /// HTTP client for the hub REST API with a client-credentials bearer token for the
    /// form-builder client. Defaults to <c>forms:read</c> when no scope is given.
    /// </summary>
    public async Task<HttpClient> CreateAuthorizedHubClientAsync(params string[] scopes)
    {
        var requested = scopes.Length == 0 ? ["forms:read"] : scopes;
        var client = _app.CreateHttpClient("hub");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetServiceTokenAsync(requested));
        return client;
    }

    /// <summary>
    /// Runs the OAuth2 client-credentials exchange against Keycloak for the form-builder
    /// client, requesting <paramref name="scopes"/> (a subset of its allowed scopes).
    /// </summary>
    public Task<string> GetServiceTokenAsync(params string[] scopes) =>
        GetServiceTokenAsync(FormBuilderClientId, FormBuilderClientSecret, scopes);

    /// <summary>
    /// HTTP client for the hub REST API with a client-credentials bearer token for the
    /// form-filler client. Defaults to its only scope, <c>published-forms:read</c>.
    /// </summary>
    public async Task<HttpClient> CreateFormFillerAuthorizedHubClientAsync(params string[] scopes)
    {
        var requested = scopes.Length == 0 ? ["published-forms:read"] : scopes;
        var client = _app.CreateHttpClient("hub");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer", await GetServiceTokenAsync(FormFillerClientId, FormFillerClientSecret, requested));
        return client;
    }

    /// <summary>
    /// Runs the OAuth2 client-credentials exchange against Keycloak for an arbitrary client,
    /// requesting <paramref name="scopes"/> (a subset of that client's allowed scopes).
    /// </summary>
    private async Task<string> GetServiceTokenAsync(string clientId, string clientSecret, string[] scopes)
    {
        // Aspire serves Keycloak over a self-signed certificate.
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        };
        using var http = new HttpClient(handler);
        using var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["scope"] = string.Join(' ', scopes),
        });

        var tokenEndpoint = new Uri(KeycloakBaseUrl, $"/realms/{Realm}/protocol/openid-connect/token");
        using var response = await http.PostAsync(tokenEndpoint, body);
        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        return token?.AccessToken
            ?? throw new InvalidOperationException("Keycloak returned an empty token response.");
    }

    public async Task InitializeAsync()
    {
        await PlaywrightInstaller.EnsureInstalledAsync();

        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.PhieuFlow_AppHost>();

        _app = await builder.BuildAsync();
        await _app.StartAsync();

        using var startupCts = new CancellationTokenSource(StartupTimeout);
        var notifications = _app.Services.GetRequiredService<ResourceNotificationService>();
        await notifications.WaitForResourceHealthyAsync("keycloak", startupCts.Token);
        await notifications.WaitForResourceHealthyAsync("hub", startupCts.Token);
        await notifications.WaitForResourceHealthyAsync("formbuilder", startupCts.Token);
        await notifications.WaitForResourceHealthyAsync("formfiller", startupCts.Token);

        FormBuilderBaseUrl = _app.GetEndpoint("formbuilder", "http");
        KeycloakBaseUrl = _app.GetEndpoint("keycloak", "http");
        FormFillerBaseUrl = _app.GetEndpoint("formfiller", "http");

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.DisposeAsync();
        }

        _playwright?.Dispose();

        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private sealed record TokenResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken);
}

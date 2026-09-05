using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace PhieuFlow.FormFiller.Clients;

/// <summary>
/// Keycloak client-credentials settings (ADR 0005). Bound from the <c>Keycloak</c>
/// configuration section; supplied by the AppHost as environment variables.
/// </summary>
public sealed class KeycloakClientOptions
{
    /// <summary>Realm authority, e.g. <c>http://localhost:8080/realms/phieuflow</c>.</summary>
    public string Authority { get; set; } = "";

    public string ClientId { get; set; } = "";

    public string ClientSecret { get; set; } = "";

    /// <summary>
    /// Scopes to request. <c>published-forms:read</c> is the only default client scope the
    /// form-filler Keycloak client has — it must never be able to read draft/current form
    /// content, only published forms.
    /// </summary>
    public string Scope { get; set; } = "published-forms:read";

    public string TokenEndpoint => $"{Authority.TrimEnd('/')}/protocol/openid-connect/token";
}

/// <summary>
/// Fetches and caches one client-credentials access token process-wide, refreshing it
/// shortly before expiry. Guarded so a burst of concurrent Hub calls triggers a single
/// token request.
/// </summary>
public sealed class ClientCredentialsTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<KeycloakClientOptions> options,
    TimeProvider timeProvider)
{
    private const int RefreshSkewSeconds = 30;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    /// <summary>Drops the cached token so the next <see cref="GetAsync"/> fetches a fresh one.</summary>
    public void Invalidate()
    {
        _token = null;
        _expiresAt = DateTimeOffset.MinValue;
    }

    public async Task<string> GetAsync(CancellationToken cancellationToken)
    {
        if (_token is not null && timeProvider.GetUtcNow() < _expiresAt)
        {
            return _token;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_token is not null && timeProvider.GetUtcNow() < _expiresAt)
            {
                return _token;
            }

            var o = options.Value;
            using var client = httpClientFactory.CreateClient("keycloak-token");
            using var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = o.ClientId,
                ["client_secret"] = o.ClientSecret,
                ["scope"] = o.Scope,
            });

            using var response = await client.PostAsync(o.TokenEndpoint, body, cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
                ?? throw new InvalidOperationException("Keycloak returned an empty token response.");

            _token = payload.AccessToken;
            _expiresAt = timeProvider.GetUtcNow().AddSeconds(Math.Max(0, payload.ExpiresIn - RefreshSkewSeconds));
            return _token;
        }
        finally
        {
            _gate.Release();
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}

/// <summary>
/// Attaches the client-credentials bearer token to every Hub request. On a 401 it drops
/// the cached token and retries once, covering a rotated secret or a Hub that came up
/// after this process cached a token signed by a now-replaced key.
/// </summary>
public sealed class ClientCredentialsTokenHandler(ClientCredentialsTokenProvider provider)
    : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Buffer any body up front so the request can be replayed after a 401.
        byte[]? body = request.Content is null
            ? null
            : await request.Content.ReadAsByteArrayAsync(cancellationToken);

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", await provider.GetAsync(cancellationToken));

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        response.Dispose();
        provider.Invalidate();

        using var retry = CloneWithBody(request, body);
        retry.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", await provider.GetAsync(cancellationToken));
        return await base.SendAsync(retry, cancellationToken);
    }

    private static HttpRequestMessage CloneWithBody(HttpRequestMessage request, byte[]? body)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy,
        };

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var option in (IDictionary<string, object?>)request.Options)
        {
            clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);
        }

        if (body is not null)
        {
            clone.Content = new ByteArrayContent(body);
            if (request.Content is not null)
            {
                foreach (var contentHeader in request.Content.Headers)
                {
                    clone.Content.Headers.TryAddWithoutValidation(contentHeader.Key, contentHeader.Value);
                }
            }
        }

        return clone;
    }
}

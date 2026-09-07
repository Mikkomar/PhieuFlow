using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PhieuFlow.Tests.Integration.Infrastructure;

/// <summary>
/// Authenticates every request as a service caller holding all Hub scopes, so the
/// integration-sql tests reach the endpoints and their persistence without minting or
/// validating a token. Token validation and scope enforcement are covered separately by
/// <see cref="HubAuthWebApplicationFactory"/> (the integration-auth tier).
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "IntegrationTest";

    // Space-delimited, matching the real token shape ScopeHandler parses.
    private const string AllScopes = "forms:read forms:write published-forms:read submissions:write";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim("scope", AllScopes),
                new Claim("sub", "integration-tests"),
                new Claim("azp", "integration-tests"),
            ],
            SchemeName);

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

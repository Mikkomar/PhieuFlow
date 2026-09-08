using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PhieuFlow.Tests.Integration.Infrastructure;

/// <summary>
/// Authenticates every request as a caller holding all Hub scopes, so the integration-sql
/// tests reach the endpoints without minting or validating a token. The integration-auth
/// tier covers token validation and scope enforcement.
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "IntegrationTest";

    // Space-delimited, matching the real token shape ScopeHandler parses.
    private const string AllScopes = "forms:read forms:write published-forms:read submissions:write submissions:read";

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

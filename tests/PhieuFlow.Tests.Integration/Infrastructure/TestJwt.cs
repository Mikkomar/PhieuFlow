using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace PhieuFlow.Tests.Integration.Infrastructure;

/// <summary>
/// Mints signed access tokens for the Hub without Keycloak. The Hub's JWT bearer is
/// rebound offline to <see cref="SigningKey"/> and <see cref="Issuer"/> by
/// <see cref="HubAuthWebApplicationFactory"/>.
/// </summary>
internal static class TestJwt
{
    public const string Issuer = "https://test-idp.phieuflow.local";
    public const string Audience = "phieuflow-hub";

    // HS256 needs >= 256 bits of key material.
    public static readonly SymmetricSecurityKey SigningKey =
        new(Encoding.UTF8.GetBytes("phieuflow-integration-test-signing-key-0001"));

    public static readonly SymmetricSecurityKey UnknownKey =
        new(Encoding.UTF8.GetBytes("phieuflow-integration-test-WRONG-key-999999"));

    /// <param name="scope">Value of the scope claim, or <c>null</c> to omit it.</param>
    /// <param name="scopeClaimName"><c>scope</c> (Keycloak) or <c>scp</c> (Entra ID).</param>
    public static string Create(
        string? scope = "forms:read",
        string scopeClaimName = "scope",
        string audience = Audience,
        string issuer = Issuer,
        DateTime? expires = null,
        SecurityKey? signingKey = null)
    {
        var claims = new Dictionary<string, object>
        {
            ["azp"] = "form-builder",
            ["sub"] = "form-builder",
        };

        if (scope is not null)
        {
            claims[scopeClaimName] = scope;
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = DateTime.UtcNow,
            NotBefore = DateTime.UtcNow,
            Expires = expires ?? DateTime.UtcNow.AddMinutes(5),
            Claims = claims,
            SigningCredentials = new SigningCredentials(
                signingKey ?? SigningKey, SecurityAlgorithms.HmacSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}

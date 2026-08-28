using Microsoft.AspNetCore.Authorization;

namespace PhieuFlow.Hub.Authorization;

/// <summary>
/// Requires a single OAuth2 scope to be present on the caller's token (ADR 0005).
/// </summary>
public sealed class ScopeRequirement(string scope) : IAuthorizationRequirement
{
    public string Scope { get; } = scope;
}

/// <summary>
/// Succeeds when the required scope appears in the token's scope claim. The claim is one
/// space-delimited string, not one claim per scope, so <c>RequireClaim</c> cannot express
/// this. Kept OIDC-generic: reads <c>scope</c> (Keycloak) and falls back to <c>scp</c>
/// (Microsoft Entra ID), so swapping the authority does not change this code (ADR 0005).
/// </summary>
public sealed class ScopeHandler : AuthorizationHandler<ScopeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, ScopeRequirement requirement)
    {
        var raw = context.User.FindFirst("scope")?.Value
                  ?? context.User.FindFirst("scp")?.Value;

        if (raw is not null && raw
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(requirement.Scope, StringComparer.Ordinal))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

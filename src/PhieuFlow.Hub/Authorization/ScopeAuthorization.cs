using Microsoft.AspNetCore.Authorization;

namespace PhieuFlow.Hub.Authorization;

/// <summary>Requires one OAuth2 scope on the caller's token.</summary>
public sealed class ScopeRequirement(string scope) : IAuthorizationRequirement
{
    public string Scope { get; } = scope;
}

/// <summary>
/// Succeeds when the required scope appears in the token's <c>scope</c> claim, a single
/// space-delimited string that <c>RequireClaim</c> cannot match. Also reads <c>scp</c> so
/// Keycloak and Entra ID both work.
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

using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authorization;
using PhieuFlow.Hub.Authorization;
using Xunit;

namespace PhieuFlow.Tests.Integration;

/// <summary>
/// Unit coverage of the space-delimited scope-claim parsing in
/// <see cref="ScopeHandler"/> (ADR 0005), independent of the HTTP pipeline.
/// </summary>
public sealed class ScopeHandlerTests
{
    [Fact]
    public async Task TestHandleRequirementAsync_When_ScopeIsPresentAmongOthers_Should_Succeed()
    {
        var context = ContextFor("forms:read forms:write submissions:write", require: "forms:write");

        await new ScopeHandler().HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task TestHandleRequirementAsync_When_ScopeClaimHasIrregularWhitespace_Should_Succeed()
    {
        var context = ContextFor("  forms:read   forms:write  ", require: "forms:write");

        await new ScopeHandler().HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task TestHandleRequirementAsync_When_ScopeIsInScpClaim_Should_Succeed()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("scp", "forms:read")], authenticationType: "Test"));
        var requirement = new ScopeRequirement("forms:read");
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        await new ScopeHandler().HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task TestHandleRequirementAsync_When_RequiredScopeIsAbsent_Should_NotSucceed()
    {
        var context = ContextFor("forms:read", require: "forms:write");

        await new ScopeHandler().HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task TestHandleRequirementAsync_When_NoScopeClaim_Should_NotSucceed()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([], authenticationType: "Test"));
        var requirement = new ScopeRequirement("forms:read");
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        await new ScopeHandler().HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task TestHandleRequirementAsync_When_ScopeIsProperPrefixOfRequired_Should_NotSucceed()
    {
        var context = ContextFor("forms:rea", require: "forms:read");

        await new ScopeHandler().HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    private static AuthorizationHandlerContext ContextFor(string scopeClaim, string require)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("scope", scopeClaim)], authenticationType: "Test"));
        var requirement = new ScopeRequirement(require);
        return new AuthorizationHandlerContext([requirement], user, resource: null);
    }
}

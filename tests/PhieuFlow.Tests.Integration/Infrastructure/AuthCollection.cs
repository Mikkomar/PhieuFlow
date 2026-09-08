using Xunit;

namespace PhieuFlow.Tests.Integration.Infrastructure;

/// <summary>
/// The integration-auth tier: auth-pipeline tests that host the Hub in-process with an
/// offline-validated JWT and in-memory SQLite. Its own collection, so it never runs in the
/// SQL-backed <see cref="IntegrationCollection"/>.
/// </summary>
[CollectionDefinition(Name)]
public sealed class AuthCollection : ICollectionFixture<HubAuthWebApplicationFactory>
{
    public const string Name = "integration-auth";
}

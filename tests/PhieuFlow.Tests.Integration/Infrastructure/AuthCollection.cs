using Xunit;

namespace PhieuFlow.Tests.Integration.Infrastructure;

/// <summary>
/// The integration-auth tier: the auth-pipeline tests that host the Hub in-process with an
/// offline-validated JWT and in-memory SQLite (no Docker). Its own collection so it is
/// never swept into the SQL-backed <see cref="IntegrationCollection"/>.
/// </summary>
[CollectionDefinition(Name)]
public sealed class AuthCollection : ICollectionFixture<HubAuthWebApplicationFactory>
{
    public const string Name = "integration-auth";
}

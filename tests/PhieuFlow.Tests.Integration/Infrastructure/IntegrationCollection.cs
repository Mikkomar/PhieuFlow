using Xunit;

namespace PhieuFlow.Tests.Integration.Infrastructure;

/// <summary>
/// The integration-sql tier. Every form-management test class runs in this one collection,
/// so xUnit runs them serially — required because they share the migrated SQL Server
/// database and the single pinned connection behind <see cref="SqlServerFixture"/>.
/// </summary>
[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "integration-sql";
}

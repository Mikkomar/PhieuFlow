using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using PhieuFlow.Tests.Integration.Infrastructure;
using Xunit;

namespace PhieuFlow.Tests.Integration;

/// <summary>
/// Guards the reason this tier exists: the schema under test is the migration chain in
/// <c>src/PhieuFlow.Persistence/Migrations</c> that <c>MigrationService</c> applied — not
/// <c>EnsureCreated()</c> — and it still matches the entity configuration.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class MigrationModelTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task TestModel_Should_HaveNoPendingMigrations()
    {
        await using var db = fixture.CreateDbContext();

        var pending = await db.Database.GetPendingMigrationsAsync();
        pending.Should().BeEmpty("MigrationService applies the whole chain before the fixture is ready");

        var applied = await db.Database.GetAppliedMigrationsAsync();
        applied.Should().NotBeEmpty("the database is built by migrations, not EnsureCreated()");
    }

    [Fact]
    public async Task TestModel_Should_HaveNoPendingModelChanges()
    {
        await using var db = fixture.CreateDbContext();

        db.Database.HasPendingModelChanges()
            .Should().BeFalse("the migration snapshot must match the current entity configuration");
    }
}

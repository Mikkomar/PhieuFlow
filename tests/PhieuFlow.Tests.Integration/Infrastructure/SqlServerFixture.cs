extern alias integrationapphost;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PhieuFlow.Persistence;
using Xunit;

namespace PhieuFlow.Tests.Integration.Infrastructure;

/// <summary>
/// The one expensive shared resource for the integration-sql tier: a real SQL Server
/// container plus the production migration chain, stood up once for the collection by
/// <see cref="DistributedApplicationTestingBuilder"/> driving
/// <c>PhieuFlow.Tests.IntegrationAppHost</c> (only <c>sql</c> and the <c>migrations</c>
/// worker — no Keycloak, no Hub container). The Hub itself is hosted in-process by
/// <see cref="IntegrationWebApplicationFactory"/> against the connection string here.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    // SQL Server image pull on a cold machine, then the migrate worker runs to completion.
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(5);

    private DistributedApplication _app = null!;

    /// <summary>Connection string for the migrated <c>PhieuFlowHub</c> database.</summary>
    public string ConnectionString { get; private set; } = null!;

    /// <summary>In-process Hub bound to <see cref="ConnectionString"/>, auth stubbed out.</summary>
    public IntegrationWebApplicationFactory Hub { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        using var startupCts = new CancellationTokenSource(StartupTimeout);

        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<integrationapphost::Projects.PhieuFlow_Tests_IntegrationAppHost>(startupCts.Token);

        _app = await builder.BuildAsync(startupCts.Token);
        await _app.StartAsync(startupCts.Token);

        var notifications = _app.Services.GetRequiredService<ResourceNotificationService>();
        var completion = await notifications.WaitForResourceAsync(
            "migrations",
            e => e.Snapshot.State?.Text is { } state && KnownResourceStates.TerminalStates.Contains(state),
            startupCts.Token);

        var exitCode = completion.Snapshot.ExitCode;
        if (completion.Snapshot.State?.Text != KnownResourceStates.Finished || exitCode is not (null or 0))
        {
            throw new InvalidOperationException(
                $"HubDatabase migration did not succeed: state='{completion.Snapshot.State?.Text}', exitCode={exitCode}. " +
                "Check the migration chain in src/PhieuFlow.Persistence/Migrations.");
        }

        ConnectionString = await _app.GetConnectionStringAsync("HubDatabase", startupCts.Token)
            ?? throw new InvalidOperationException("Aspire returned no connection string for HubDatabase.");

        Hub = new IntegrationWebApplicationFactory(ConnectionString);
        _ = Hub.Services; // Build the host now so a bad configuration fails the fixture, not the first test.
    }

    public async Task DisposeAsync()
    {
        if (Hub is not null)
        {
            await Hub.DisposeAsync();
        }

        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    /// <summary>
    /// A throwaway context on a pooled connection (not the pinned one in
    /// <see cref="IntegrationWebApplicationFactory"/>). Use outside any ambient transaction —
    /// for the migration guard test and <see cref="ResetAsync"/>.
    /// </summary>
    internal HubDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<HubDbContext>().UseSqlServer(ConnectionString).Options);

    /// <summary>
    /// Deletes all form data. Not the per-test isolation mechanism — that is the
    /// <see cref="System.Transactions.TransactionScope"/> in <see cref="IntegrationTestBase"/> —
    /// but an opt-out for a test that deliberately commits and must clean up after itself.
    /// <c>Forms</c> cascades versions → pages → questions → options; submissions are
    /// <c>Restrict</c> and must go first.
    /// </summary>
    public async Task ResetAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM [SubmissionAnswers]; DELETE FROM [FormSubmissions]; DELETE FROM [Forms];");
    }
}

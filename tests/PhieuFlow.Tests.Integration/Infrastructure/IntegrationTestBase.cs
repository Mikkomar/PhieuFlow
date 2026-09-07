using System.Transactions;
using Xunit;

namespace PhieuFlow.Tests.Integration.Infrastructure;

/// <summary>
/// Base for every integration-sql test class. Each test runs inside a
/// <see cref="TransactionScope"/> that is disposed without <c>Complete()</c>, so every
/// write — the test's and the in-process Hub's, which share <see cref="SqlServerFixture"/>'s
/// single connection — rolls back and the next test starts from the migrated-but-empty
/// schema. Nothing is committed, so there is no cleanup step.
/// </summary>
[Collection(IntegrationCollection.Name)]
public abstract class IntegrationTestBase(SqlServerFixture fixture) : IAsyncLifetime
{
    private TransactionScope _scope = null!;

    protected IntegrationWebApplicationFactory Factory => fixture.Hub;

    /// <summary>Fresh unauthenticated-looking client; <see cref="TestAuthHandler"/> authorizes it.</summary>
    protected HttpClient CreateClient() => fixture.Hub.CreateClient();

    /// <summary>Root DI container of the in-process Hub — for tests that call a repository directly.</summary>
    protected IServiceProvider Services => fixture.Hub.Services;

    public Task InitializeAsync()
    {
        // Async flow so Transaction.Current propagates into the in-process TestServer
        // pipeline (which runs on the caller's execution context — no real socket).
        _scope = new TransactionScope(
            TransactionScopeOption.Required,
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
            TransactionScopeAsyncFlowOption.Enabled);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        // No Complete() — roll back everything the test and the Hub wrote.
        _scope.Dispose();
        return Task.CompletedTask;
    }
}

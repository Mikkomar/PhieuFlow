using System.Net.Http.Json;
using AwesomeAssertions;
using PhieuFlow.Hub.Contracts.Forms;
using PhieuFlow.Tests.Integration.Infrastructure;
using Xunit;

namespace PhieuFlow.Tests.Integration;

/// <summary>
/// Guards the isolation mechanism: every other test in this tier writes forms, and every
/// one is wrapped in a <see cref="System.Transactions.TransactionScope"/> that rolls back.
/// If that ever stops working, this test (run among ~50 others that create rows) sees the
/// leaked rows.
/// </summary>
public sealed class TransactionIsolationTests(SqlServerFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task TestRollback_Should_LeaveNoRowsFromEarlierTests()
    {
        using var client = CreateClient();

        var listing = await client.GetFromJsonAsync<FormBatchResponse>("/forms?take=100");

        listing!.Items.Should().BeEmpty("each test's writes roll back when its TransactionScope is disposed");
    }

    [Fact]
    public async Task TestRollback_When_AFormWasCreatedInThisTest_Should_NotSurviveIntoTheNext()
    {
        using var client = CreateClient();
        await client.PostAsync("/forms", content: null);

        var listing = await client.GetFromJsonAsync<FormBatchResponse>("/forms?take=100");

        // Visible inside the test's own transaction...
        listing!.Items.Should().ContainSingle();
        // ...and TestRollback_Should_LeaveNoRowsFromEarlierTests proves it does not outlive it.
    }
}

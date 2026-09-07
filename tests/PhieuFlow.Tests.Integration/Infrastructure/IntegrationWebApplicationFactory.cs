using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PhieuFlow.Hub.Submissions;
using PhieuFlow.Persistence;
using RabbitMQ.Client;

namespace PhieuFlow.Tests.Integration.Infrastructure;

/// <summary>
/// Hosts the real Hub in-process against the migrated SQL Server database
/// <see cref="SqlServerFixture"/> stood up. Two swaps:
///
/// <list type="bullet">
/// <item>Authentication is replaced with <see cref="TestAuthHandler"/> — every request is
/// authenticated with all scopes, so the endpoints and their persistence are what is under
/// test, not token validation.</item>
/// <item><see cref="HubDbContext"/> is re-registered onto a single shared
/// <see cref="SqlConnection"/> instance (EF opens/closes it per operation). One physical
/// connection means a per-test <see cref="System.Transactions.TransactionScope"/> stays a
/// lightweight transaction and rolls back without MSDTC — which is absent on Linux/CI.</item>
/// <item><c>TestServer.PreserveExecutionContext</c> is turned on so the test's ambient
/// <see cref="System.Transactions.Transaction.Current"/> flows into the in-process request
/// pipeline — without it TestHost suppresses the execution context and the shared
/// connection never enlists, so the server's writes commit and leak across tests.</item>
/// </list>
/// </summary>
public sealed class IntegrationWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    private readonly SqlConnection _connection = new(connectionString);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // AddSqlServerDbContext reads this at registration time; the provider is re-pinned
        // to the shared connection below, so the value only has to be present and valid.
        builder.UseSetting("ConnectionStrings:HubDatabase", connectionString);

        builder.ConfigureTestServices(services =>
        {
            services.Configure<TestServerOptions>(o => o.PreserveExecutionContext = true);

            RemoveHubDbContext(services);
            services.AddDbContext<HubDbContext>(options => options.UseSqlServer(_connection));

            // This tier's AppHost has no broker. Drop the RabbitMQ consumer and its
            // connection so host startup does not try to dial one; SubmissionConsumeTests
            // drives SubmissionMessageHandler directly, which needs neither.
            RemoveSubmissionConsumer(services);

            // Last AddAuthentication wins for the default scheme, so every RequireAuthorization
            // policy authenticates against TestAuthHandler. The Hub's JwtBearer registration
            // stays wired but is never exercised.
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    private static void RemoveSubmissionConsumer(IServiceCollection services)
    {
        var doomed = services.Where(d =>
                d.ImplementationType == typeof(SubmissionConsumerService)
                || d.ServiceType == typeof(IConnection)
                || d.ServiceType == typeof(IConnectionFactory))
            .ToList();

        foreach (var descriptor in doomed)
        {
            services.Remove(descriptor);
        }
    }

    private static void RemoveHubDbContext(IServiceCollection services)
    {
        var doomed = services.Where(d =>
                d.ServiceType == typeof(HubDbContext)
                || d.ServiceType == typeof(DbContextOptions)
                || (d.ServiceType.IsGenericType
                    && d.ServiceType.GetGenericArguments().Contains(typeof(HubDbContext))))
            .ToList();

        foreach (var descriptor in doomed)
        {
            services.Remove(descriptor);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}

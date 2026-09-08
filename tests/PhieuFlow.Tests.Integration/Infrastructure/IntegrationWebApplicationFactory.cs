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
/// Hosts the Hub in-process against the migrated SQL Server database. Authentication is
/// <see cref="TestAuthHandler"/> (all scopes); <see cref="HubDbContext"/> shares one
/// connection so a per-test <c>TransactionScope</c> rolls back without MSDTC.
/// </summary>
public sealed class IntegrationWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    private readonly SqlConnection _connection = new(connectionString);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // AddSqlServerDbContext reads this at registration. The provider is re-pinned to the
        // shared connection below, so the value only has to be present and valid.
        builder.UseSetting("ConnectionStrings:HubDatabase", connectionString);

        builder.ConfigureTestServices(services =>
        {
            services.Configure<TestServerOptions>(o => o.PreserveExecutionContext = true);

            RemoveHubDbContext(services);
            services.AddDbContext<HubDbContext>(options => options.UseSqlServer(_connection));

            // This tier has no broker. Drop the RabbitMQ consumer so host startup does not
            // dial one. SubmissionConsumeTests drives SubmissionMessageHandler directly.
            RemoveSubmissionConsumer(services);

            // Last AddAuthentication wins the default scheme, so every RequireAuthorization
            // policy runs against TestAuthHandler. The Hub's JwtBearer stays wired but unused.
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

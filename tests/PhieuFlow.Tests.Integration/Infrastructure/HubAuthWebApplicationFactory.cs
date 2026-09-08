using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using PhieuFlow.Hub.Submissions;
using PhieuFlow.Persistence;
using RabbitMQ.Client;

namespace PhieuFlow.Tests.Integration.Infrastructure;

/// <summary>
/// Hosts the real Hub in-process with two swaps: JWT bearer validates
/// <see cref="TestJwt"/>-signed tokens offline, and <see cref="HubDbContext"/> runs on a
/// private in-memory SQLite database so the write path round-trips without a container.
/// </summary>
public sealed class HubAuthWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public HubAuthWebApplicationFactory()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // AddSqlServerDbContext reads this at registration. The value is unused because the
        // provider is swapped below.
        builder.UseSetting("ConnectionStrings:HubDatabase", "Server=unused;Database=unused");

        builder.ConfigureTestServices(services =>
        {
            RemoveHubDbContext(services);
            services.AddDbContext<HubDbContext>(options => options.UseSqlite(_connection));

            // No broker in this tier. Drop the RabbitMQ consumer so host startup does not
            // dial one. This tier only exercises the auth boundary.
            RemoveSubmissionConsumer(services);

            RebindJwtBearerOffline(services);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<HubDbContext>().Database.EnsureCreated();

        return host;
    }

    public HttpClient CreateClientWithToken(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
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

    private static void RebindJwtBearerOffline(IServiceCollection services)
    {
        services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            // No Authority or MetadataAddress: the post-configure step skips building a
            // ConfigurationManager and the handler validates against options.Configuration.
            options.Authority = null;
            options.RequireHttpsMetadata = false;

            options.Configuration = new OpenIdConnectConfiguration { Issuer = TestJwt.Issuer };
            options.Configuration.SigningKeys.Add(TestJwt.SigningKey);

            var validation = options.TokenValidationParameters;
            validation.ValidateIssuer = true;
            validation.ValidIssuer = TestJwt.Issuer;
            validation.ValidateAudience = true;
            validation.ValidAudience = TestJwt.Audience;
            validation.ValidateIssuerSigningKey = true;
            validation.IssuerSigningKey = TestJwt.SigningKey;
            validation.ValidateLifetime = true;
            validation.ClockSkew = TimeSpan.Zero;
        });
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

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PhieuFlow.Hub.Data;

namespace PhieuFlow.MigrationService;

public class Worker(
    IServiceProvider serviceProvider,
    IHostApplicationLifetime hostApplicationLifetime,
    ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<HubDbContext>();

            // AddSqlServerDbContext enables EnableRetryOnFailure by default, so MigrateAsync
            // (which opens its own transaction) must run through the execution strategy or
            // EF Core throws at runtime.
            var strategy = dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(() => dbContext.Database.MigrateAsync(stoppingToken));

            logger.LogInformation("HubDatabase migrations applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while migrating HubDatabase.");

            // BackgroundService's default fault handling stops the host but doesn't set a
            // non-zero exit code, which WaitForCompletion(migrations) in the AppHost would
            // otherwise read as success.
            Environment.ExitCode = 1;
        }
        finally
        {
            hostApplicationLifetime.StopApplication();
        }
    }
}

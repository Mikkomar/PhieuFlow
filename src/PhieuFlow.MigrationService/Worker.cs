using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PhieuFlow.Persistence;

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

            // EnableRetryOnFailure is on, so MigrateAsync (which opens its own transaction)
            // must run through the execution strategy, or EF Core throws.
            var strategy = dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(() => dbContext.Database.MigrateAsync(stoppingToken));

            logger.LogInformation("HubDatabase migrations applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while migrating HubDatabase.");

            // BackgroundService stops the host on fault but sets no exit code, so the
            // AppHost's WaitForCompletion(migrations) would read the failure as success.
            Environment.ExitCode = 1;
        }
        finally
        {
            hostApplicationLifetime.StopApplication();
        }
    }
}

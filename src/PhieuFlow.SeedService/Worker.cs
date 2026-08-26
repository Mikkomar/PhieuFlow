using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PhieuFlow.Persistence;
using PhieuFlow.SeedService.SampleData;

namespace PhieuFlow.SeedService;

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

            if (await dbContext.Forms.AnyAsync(stoppingToken))
            {
                logger.LogInformation("HubDatabase already has forms; skipping seed.");
            }
            else
            {
                dbContext.Forms.AddRange(SampleForms.All());
                await dbContext.SaveChangesAsync(stoppingToken);
                logger.LogInformation("HubDatabase seeded with sample forms.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding HubDatabase.");

            // BackgroundService's default fault handling stops the host but doesn't set a
            // non-zero exit code, which WaitForCompletion(seed) in the AppHost would
            // otherwise read as success.
            Environment.ExitCode = 1;
        }
        finally
        {
            hostApplicationLifetime.StopApplication();
        }
    }
}

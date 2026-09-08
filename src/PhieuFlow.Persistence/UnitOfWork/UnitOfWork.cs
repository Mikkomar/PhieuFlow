using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PhieuFlow.Persistence.Repositories;

namespace PhieuFlow.Persistence.UnitOfWork;

public class UnitOfWork(HubDbContext dbContext, IFormRepository forms, ILogger<UnitOfWork> logger) : IUnitOfWork
{
    public IFormRepository Forms { get; } = forms;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Restrict FKs, unique-index clashes and concurrency-token mismatches all
            // surface here. Otherwise they reach the caller as a bare 500.
            logger.LogError(ex, "Persisting changes to HubDatabase failed.");
            throw;
        }
    }
}

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
            // Restrict FKs (a form with submissions), unique-index clashes, concurrency
            // token mismatches — all surface here and otherwise reach the caller as a bare 500.
            logger.LogError(ex, "Persisting changes to HubDatabase failed.");
            throw;
        }
    }
}

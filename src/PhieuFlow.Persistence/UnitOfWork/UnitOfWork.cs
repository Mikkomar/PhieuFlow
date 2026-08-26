using PhieuFlow.Persistence.Repositories;

namespace PhieuFlow.Persistence.UnitOfWork;

public class UnitOfWork(HubDbContext dbContext, IFormRepository forms) : IUnitOfWork
{
    public IFormRepository Forms { get; } = forms;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

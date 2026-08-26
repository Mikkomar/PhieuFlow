using PhieuFlow.Persistence.Repositories;

namespace PhieuFlow.Persistence.UnitOfWork;

public interface IUnitOfWork
{
    IFormRepository Forms { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

using PhieuFlow.Persistence.Projections;

namespace PhieuFlow.Persistence.Repositories;

public interface IFormRepository
{
    Task<FormBatchResult> GetBatchAsync(Guid? startId, int take, CancellationToken cancellationToken = default);
}

using PhieuFlow.Core.Entities;
using PhieuFlow.Persistence.Projections;

namespace PhieuFlow.Persistence.Repositories;

public interface IFormRepository
{
    Task<FormBatchResult> GetBatchAsync(Guid? startId, int take, CancellationToken cancellationToken = default);

    Task<Form?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveAsync(Form form, CancellationToken cancellationToken = default);
}

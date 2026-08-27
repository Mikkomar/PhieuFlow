using PhieuFlow.Core.Entities;
using PhieuFlow.Persistence.Projections;

namespace PhieuFlow.Persistence.Repositories;

public interface IFormRepository
{
    Task<FormBatchResult> GetBatchAsync(Guid? startId, int take, CancellationToken cancellationToken = default);

    Task<FormVersion?> GetByIdAsync(Guid formId, CancellationToken cancellationToken = default);

    Task SaveAsync(Guid formId, FormVersion incomingContent, CancellationToken cancellationToken = default);

    Task<bool> PublishAsync(Guid formId, CancellationToken cancellationToken = default);
}

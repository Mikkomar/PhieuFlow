using PhieuFlow.Core.Entities;
using PhieuFlow.Persistence.Projections;

namespace PhieuFlow.Persistence.Repositories;

public interface IFormRepository
{
    Task<FormBatchResult> GetBatchAsync(Guid? startId, int take, CancellationToken cancellationToken = default);

    Task<FormVersion?> GetByIdAsync(Guid formId, CancellationToken cancellationToken = default);

    Task<FormVersionState> SaveAsync(Guid formId, FormVersion incomingContent, CancellationToken cancellationToken = default);

    Task<FormVersionState?> PublishAsync(Guid formId, CancellationToken cancellationToken = default);
}

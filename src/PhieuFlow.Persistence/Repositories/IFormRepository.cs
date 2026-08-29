using PhieuFlow.Core.Entities;
using PhieuFlow.Persistence.Projections;

namespace PhieuFlow.Persistence.Repositories;

public interface IFormRepository
{
    Task<FormBatchResult> GetBatchAsync(Guid? startId, int take, CancellationToken cancellationToken = default);

    /// <summary>Persists a blank draft (v1, one empty page) and returns its form id.</summary>
    Task<Guid> CreateAsync(CancellationToken cancellationToken = default);

    Task<FormVersion?> GetByIdAsync(Guid formId, CancellationToken cancellationToken = default);

    Task<FormVersionState> SaveAsync(Guid formId, FormVersion incomingContent, CancellationToken cancellationToken = default);

    Task<FormVersionState?> PublishAsync(Guid formId, CancellationToken cancellationToken = default);

    Task<int?> GetLatestPublishedVersionNumberAsync(Guid formId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the form and its whole version tree (cascade). Returns <c>false</c> when no
    /// form has that id, so the caller can 404 rather than reporting a phantom success.
    /// </summary>
    Task<bool> DeleteAsync(Guid formId, CancellationToken cancellationToken = default);
}

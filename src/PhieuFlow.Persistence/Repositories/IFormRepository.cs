using PhieuFlow.Core.Entities;
using PhieuFlow.Persistence.Projections;

namespace PhieuFlow.Persistence.Repositories;

public interface IFormRepository
{
    Task<FormBatchResult> GetBatchAsync(Guid? startId, int take, CancellationToken cancellationToken = default);

    /// <summary>Persists a blank draft (v1, one empty page) and returns its form id.</summary>
    Task<Guid> CreateAsync(CancellationToken cancellationToken = default);

    Task<FormVersion?> GetByIdAsync(Guid formId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies the incoming draft content to the form's latest version, forking a new draft when
    /// the latest version is published. Returns <c>null</c> when no form has that id, so the caller
    /// can 404 rather than resurrecting a deleted form — creation is <c>POST /forms</c> only.
    /// </summary>
    Task<FormVersionState?> SaveAsync(Guid formId, FormVersion incomingContent, CancellationToken cancellationToken = default);

    Task<FormVersionState?> PublishAsync(Guid formId, CancellationToken cancellationToken = default);

    Task<int?> GetLatestPublishedVersionNumberAsync(Guid formId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the form and its whole version tree (cascade). Returns <c>false</c> when no
    /// form has that id, so the caller can 404 rather than reporting a phantom success.
    /// </summary>
    Task<bool> DeleteAsync(Guid formId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deep-copies <paramref name="sourceId"/>'s latest version into a brand-new form (v1, draft,
    /// "Copy of" title, fresh ids throughout). Returns the new form's id, or <c>null</c> when the
    /// source doesn't exist.
    /// </summary>
    Task<Guid?> DuplicateAsync(Guid sourceId, CancellationToken cancellationToken = default);
}

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
    /// the latest version is published. Returns <see cref="FormSaveStatus.FormNotFound"/> when no
    /// form has that id (the caller 404s — creation is <c>POST /forms</c> only), and
    /// <see cref="FormSaveStatus.RevisionMismatch"/> when the incoming <c>VersionNumber</c>/
    /// <c>Revision</c> no longer matches the persisted row (the caller 409s and nothing is written).
    /// </summary>
    Task<FormSaveResult> SaveAsync(Guid formId, FormVersion incomingContent, CancellationToken cancellationToken = default);

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

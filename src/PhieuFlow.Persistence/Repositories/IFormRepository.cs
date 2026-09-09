using PhieuFlow.Core.Entities;
using PhieuFlow.Persistence.Projections;

namespace PhieuFlow.Persistence.Repositories;

public interface IFormRepository
{
    Task<FormBatchResult> GetBatchAsync(Guid? startId, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Batches one form's submissions, keyset-paged by submission id like
    /// <see cref="GetBatchAsync"/>. Each answer is a display string, choice labels resolved
    /// from the published version. Returns <c>null</c> when no form has that id.
    /// </summary>
    Task<SubmissionBatchResult?> GetSubmissionsBatchAsync(Guid formId, Guid? startId, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Batches only forms with a published version, projecting that version's own Title and
    /// Description, never the current draft's. Used by the respondent-facing form-filler,
    /// which must never see draft content.
    /// </summary>
    Task<PublishedFormBatchResult> GetPublishedBatchAsync(Guid? startId, int take, CancellationToken cancellationToken = default);

    /// <summary>Persists a blank draft (v1, one empty page) and returns its form id.</summary>
    Task<Guid> CreateAsync(CancellationToken cancellationToken = default);

    Task<FormVersion?> GetByIdAsync(Guid formId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the full page and question tree of the form's published version, not the
    /// current draft. Returns <c>null</c> when the form does not exist or was never
    /// published. The form-filler must not tell the two apart.
    /// </summary>
    Task<FormVersion?> GetPublishedByIdAsync(Guid formId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the full tree of one published version, by <paramref name="versionNumber"/>.
    /// Returns <c>null</c> when the form has no published row with that number. The
    /// submission consumer uses it to re-validate an inbound response.
    /// </summary>
    Task<FormVersion?> GetPublishedVersionAsync(Guid formId, int versionNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the full tree of one version, by <paramref name="versionNumber"/>, of any
    /// status (draft or published). Returns <c>null</c> when the form has no row with that
    /// number. The builder uses it to read a past version for display.
    /// </summary>
    Task<FormVersion?> GetVersionAsync(Guid formId, int versionNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies the incoming draft content to the latest version, forking a new draft when it
    /// is published. Returns <c>FormNotFound</c> when no form has that id, or
    /// <c>RevisionMismatch</c> when the incoming version or revision is stale.
    /// </summary>
    Task<FormSaveResult> SaveAsync(Guid formId, FormVersion incomingContent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Flips exactly the row named by <paramref name="expectedVersionNumber"/> and
    /// <paramref name="expectedRevision"/> to Published. Returns <c>RevisionMismatch</c> when
    /// the server's current row has moved on.
    /// </summary>
    Task<FormPublishResult> PublishAsync(Guid formId, int expectedVersionNumber, int expectedRevision, CancellationToken cancellationToken = default);

    Task<int?> GetLatestPublishedVersionNumberAsync(Guid formId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the form and its whole version tree. Returns <c>FormNotFound</c> when no form
    /// has that id, or <c>HasSubmissions</c> when a submission (a historical record with
    /// <c>Restrict</c> FKs) blocks the delete.
    /// </summary>
    Task<FormDeleteResult> DeleteAsync(Guid formId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deep-copies <paramref name="sourceId"/>'s latest version into a new form (v1, draft,
    /// "Copy of" title, fresh ids). Returns the new form's id, or <c>null</c> when the source
    /// does not exist.
    /// </summary>
    Task<Guid?> DuplicateAsync(Guid sourceId, CancellationToken cancellationToken = default);
}

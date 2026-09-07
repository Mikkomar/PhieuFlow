using PhieuFlow.Hub.Contracts.Publishing;

namespace PhieuFlow.FormFiller.Clients;

/// <summary>
/// Transport-level access to the hub's published-forms REST API (ADR 0001). Form-filler is
/// respondent-facing and read-only: unlike the builder's client, there is no create/save/
/// publish/delete here, and the underlying Keycloak scope (<c>published-forms:read</c>) means
/// the Hub would reject those calls anyway.
/// </summary>
public interface IHubFormsClient
{
    /// <summary>Streams the published-forms list one server-fetched batch at a time, so callers can render as data arrives.</summary>
    IAsyncEnumerable<List<PublishedFormListItemDto>> GetPublishedFormBatchesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches one form's published version with its full page/question tree. Returns
    /// <c>null</c> uniformly when the form doesn't exist or has never been published — the
    /// caller can't and shouldn't distinguish the two (design's "unavailable" state).
    /// </summary>
    Task<PublishedFormDto?> GetPublishedFormByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

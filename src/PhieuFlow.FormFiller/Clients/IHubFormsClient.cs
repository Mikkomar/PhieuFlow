using PhieuFlow.Hub.Contracts.Publishing;

namespace PhieuFlow.FormFiller.Clients;

/// <summary>
/// Transport-level access to the hub's published-forms REST API. Read-only: no create,
/// save, publish or delete, and the <c>published-forms:read</c> scope would block them.
/// </summary>
public interface IHubFormsClient
{
    /// <summary>Streams the published-forms list one server batch at a time.</summary>
    IAsyncEnumerable<List<PublishedFormListItemDto>> GetPublishedFormBatchesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches one form's published version with its full page and question tree. Returns
    /// <c>null</c> when the form does not exist or was never published. The caller treats
    /// both as "unavailable".
    /// </summary>
    Task<PublishedFormDto?> GetPublishedFormByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

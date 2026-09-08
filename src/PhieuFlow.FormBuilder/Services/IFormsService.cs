using PhieuFlow.FormBuilder.Models;
using PhieuFlow.FormBuilder.Models.Editing;
using PhieuFlow.Hub.Contracts.Forms;
using PhieuFlow.Hub.Contracts.Publishing;

namespace PhieuFlow.FormBuilder.Services;

public interface IFormsService
{
    /// <summary>
    /// Asks the Hub to create a blank draft and returns its id. The builder then opens it by
    /// id, so a reload re-loads the same form.
    /// </summary>
    Task<Guid> CreateNewAsync(CancellationToken cancellationToken = default);

    /// <summary>Streams the forms list one server batch at a time.</summary>
    IAsyncEnumerable<List<FormSummary>> GetAllStreamingAsync(CancellationToken cancellationToken = default);

    /// <summary>Streams one form's submissions a batch at a time, mapped to <see cref="FormResponse"/>.</summary>
    IAsyncEnumerable<List<FormResponse>> GetSubmissionsStreamingAsync(Guid formId, CancellationToken cancellationToken = default);

    Task<FormEditModel?> GetByIdAsync(Guid formId, CancellationToken cancellationToken = default);

    Task<FormVersionStateDto> SaveAsync(FormEditModel form, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the pre-publish gate against the persisted latest version. Callers flush any
    /// pending save first so the server is current.
    /// </summary>
    Task<PublishResultDto> PublishAsync(Guid formId, CancellationToken cancellationToken = default);

    /// <summary>
    /// After a save forked a new draft, re-fetches the forked tree and copies its node ids
    /// onto <paramref name="local"/> by position, leaving its content untouched. Returns old
    /// page id to new page id, or empty if the re-fetch failed.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, Guid>> ReconcileForkAsync(FormEditModel local, CancellationToken cancellationToken = default);

    /// <summary>Deletes the form and its version history. A missing form is treated as already gone.</summary>
    Task DeleteAsync(Guid formId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a form from a deep copy of <paramref name="sourceId"/>'s latest version (new
    /// ids, "Copy of" title, draft status) and returns its id. The caller re-reads the list.
    /// </summary>
    Task<Guid> DuplicateAsync(Guid sourceId, CancellationToken cancellationToken = default);
}

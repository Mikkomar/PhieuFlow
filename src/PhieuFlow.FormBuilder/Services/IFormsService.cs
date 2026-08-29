using PhieuFlow.FormBuilder.Models;
using PhieuFlow.FormBuilder.Models.Editing;
using PhieuFlow.Hub.Contracts;

namespace PhieuFlow.FormBuilder.Services;

public interface IFormsService
{
    /// <summary>
    /// Asks the Hub to create a blank draft and returns its id. The builder then opens it by
    /// id, so a reload re-loads the same form.
    /// </summary>
    Task<Guid> CreateNewAsync(CancellationToken cancellationToken = default);

    Task<List<FormSummary>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<FormEditModel?> GetByIdAsync(Guid formId, CancellationToken cancellationToken = default);

    Task<FormVersionStateDto> SaveAsync(FormEditModel form, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the pre-publish gate against the persisted latest version. Callers flush any
    /// pending save first so the server is current.
    /// </summary>
    Task<PublishResultDto> PublishAsync(Guid formId, CancellationToken cancellationToken = default);

    /// <summary>
    /// After a save that forked a new draft (editing a published version does this server-side
    /// with fresh node ids), re-fetches the forked tree and copies its ids onto <paramref name="local"/>
    /// by position — content in <paramref name="local"/> (including edits made while the save was
    /// in flight) is left untouched. Returns old page id → new page id so the caller can keep the
    /// same page selected; empty if the re-fetch could not be completed.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, Guid>> ReconcileForkAsync(FormEditModel local, CancellationToken cancellationToken = default);

    /// <summary>Deletes the form and its version history. A missing form is treated as already gone.</summary>
    Task DeleteAsync(Guid formId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mints a fresh form seeded with a deep copy of <paramref name="sourceId"/>'s latest
    /// version (new ids throughout, "Copy of" title, back to draft) and returns its id. The
    /// caller re-reads the list to pick up the new row.
    /// </summary>
    Task<Guid> DuplicateAsync(Guid sourceId, CancellationToken cancellationToken = default);
}

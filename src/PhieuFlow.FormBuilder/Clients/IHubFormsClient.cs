using PhieuFlow.Hub.Contracts.Forms;
using PhieuFlow.Hub.Contracts.Publishing;
using PhieuFlow.Hub.Contracts.Submissions;

namespace PhieuFlow.FormBuilder.Clients;

/// <summary>
/// Transport-level access to the hub's form-management REST API. Returns raw
/// <c>PhieuFlow.Hub.Contracts</c> DTOs. <see cref="Services.FormsService"/> maps to entities.
/// </summary>
public interface IHubFormsClient
{
    /// <summary>Asks the Hub to create a blank draft and returns its id.</summary>
    Task<Guid> CreateFormAsync(CancellationToken cancellationToken = default);

    Task<FormDto?> GetFormByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<FormVersionStateDto> SaveFormAsync(FormDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the publish gate against the persisted latest version. A validation failure
    /// returns a normal <see cref="PublishResultDto"/> with <c>Published</c> false and the
    /// tree annotated.
    /// </summary>
    Task<PublishResultDto> PublishFormAsync(Guid formId, CancellationToken cancellationToken = default);

    /// <summary>Streams the forms list one server batch at a time.</summary>
    IAsyncEnumerable<List<FormListItemDto>> GetFormBatchesAsync(CancellationToken cancellationToken = default);

    /// <summary>Streams one form's submissions a batch at a time, keyset-paged.</summary>
    IAsyncEnumerable<List<SubmissionListItemDto>> GetFormSubmissionBatchesAsync(Guid formId, CancellationToken cancellationToken = default);

    /// <summary>Deletes the form and its version history. A missing form is treated as already gone.</summary>
    Task DeleteFormAsync(Guid formId, CancellationToken cancellationToken = default);

    /// <summary>Deep-copies a form's latest version into a new draft and returns the new id.</summary>
    Task<Guid> DuplicateFormAsync(Guid sourceId, CancellationToken cancellationToken = default);
}

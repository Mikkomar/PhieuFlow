using PhieuFlow.Hub.Contracts;

namespace PhieuFlow.FormBuilder.Clients;

/// <summary>
/// Transport-level access to the hub's form-management REST API (ADR 0001). Exposes the
/// raw <c>PhieuFlow.Hub.Contracts</c> DTOs; entity mapping is <see cref="Services.FormsService"/>'s job.
/// </summary>
public interface IHubFormsClient
{
    /// <summary>Asks the Hub to mint a blank draft; returns its id.</summary>
    Task<Guid> CreateFormAsync(CancellationToken cancellationToken = default);

    Task<FormDto?> GetFormByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<FormVersionStateDto> SaveFormAsync(FormDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the publish gate against the persisted latest version (callers flush any pending
    /// save first). A validation failure comes back as a normal <see cref="PublishResultDto"/>
    /// with <see cref="PublishResultDto.Published"/> false and the tree annotated.
    /// </summary>
    Task<PublishResultDto> PublishFormAsync(Guid formId, CancellationToken cancellationToken = default);

    Task<List<FormListItemDto>> GetAllFormsAsync(CancellationToken cancellationToken = default);

    /// <summary>Deletes the form and its version history. A missing form is treated as already gone.</summary>
    Task DeleteFormAsync(Guid formId, CancellationToken cancellationToken = default);
}

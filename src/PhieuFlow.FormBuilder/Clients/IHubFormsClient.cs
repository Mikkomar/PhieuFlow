using PhieuFlow.Hub.Contracts;

namespace PhieuFlow.FormBuilder.Clients;

/// <summary>
/// Transport-level access to the hub's form-management REST API (ADR 0001). Exposes the
/// raw <c>PhieuFlow.Hub.Contracts</c> DTOs; entity mapping is <see cref="Services.FormsService"/>'s job.
/// </summary>
public interface IHubFormsClient
{
    Task<FormDto?> GetFormByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<FormVersionStateDto> SaveFormAsync(FormDto dto, CancellationToken cancellationToken = default);

    Task<FormVersionStateDto> PublishFormAsync(Guid formId, CancellationToken cancellationToken = default);

    Task<List<FormListItemDto>> GetAllFormsAsync(CancellationToken cancellationToken = default);
}

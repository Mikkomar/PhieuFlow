using PhieuFlow.Core.Entities;
using PhieuFlow.Hub.Contracts.Forms;

namespace PhieuFlow.Hub.Mapping;

/// <summary>
/// Maps an incoming <see cref="FormDto"/> onto a fresh <see cref="FormVersion"/> entity.
/// It does not map Status inbound. The server owns Status.
/// </summary>
internal static class FormRequestMapper
{
    public static FormVersion ToEntity(FormDto dto, Guid formId) => new()
    {
        Id = Guid.NewGuid(),
        FormId = formId,
        // On PUT these hold the client's expected version and revision for the SaveAsync
        // concurrency check. They are not persisted.
        VersionNumber = dto.VersionNumber,
        Revision = dto.Revision,
        Title = dto.Title,
        Description = dto.Description,
        // TODO: the calling client id is now available via User.FindFirst("azp"). Per-user
        // ownership is out of scope for now, so this stays free text from the DTO.
        LastModifiedBy = dto.LastModifiedBy,
        Pages = dto.Pages.Select((p, index) => ToEntity(p, formId, index)).ToList(),
    };

    private static FormPage ToEntity(FormPageDto dto, Guid formVersionId, int order) => new()
    {
        Id = dto.Id,
        FormVersionId = formVersionId,
        Title = dto.Title,
        Order = order,
        Questions = dto.Questions.Select((q, index) => QuestionMapper.ToEntity(q, dto.Id, index)).ToList(),
    };
}

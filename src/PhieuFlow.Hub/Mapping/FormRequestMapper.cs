using PhieuFlow.Core.Entities;
using PhieuFlow.Hub.Contracts;

namespace PhieuFlow.Hub.Mapping;

/// <summary>
/// Maps an incoming <see cref="FormDto"/> onto a fresh <see cref="FormVersion"/> entity.
/// Status is intentionally not mapped inbound — the server owns it.
/// </summary>
internal static class FormRequestMapper
{
    public static FormVersion ToEntity(FormDto dto, Guid formId) => new()
    {
        Id = Guid.NewGuid(),
        FormId = formId,
        Title = dto.Title,
        Description = dto.Description,
        // TODO ADR-0005: the calling client id is now available via User.FindFirst("azp").
        // End-user identity / per-user ownership is out of ADR 0005 scope, so this stays
        // free text from the DTO for now.
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

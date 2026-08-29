using PhieuFlow.Core.Entities;
using PhieuFlow.Hub.Contracts;

namespace PhieuFlow.Hub.Mapping;

/// <summary>
/// Maps persisted form entities onto the wire <see cref="FormDto"/> tree returned to callers.
/// Callers set <see cref="FormDto.LatestPublishedVersionNumber"/> after mapping.
/// </summary>
internal static class FormResponseMapper
{
    public static FormDto ToDto(FormVersion version) => new()
    {
        Id = version.FormId,
        Title = version.Title,
        Description = version.Description,
        CreatedAt = version.CreatedAt,
        LastModifiedAt = version.LastModifiedAt,
        LastModifiedBy = version.LastModifiedBy,
        Revision = version.Revision,
        VersionNumber = version.VersionNumber,
        Status = ToDto(version.Status),
        Pages = version.Pages.Select(ToDto).ToList(),
    };

    public static FormVersionStatusDto ToDto(FormVersionStatus status) => status switch
    {
        FormVersionStatus.Draft => FormVersionStatusDto.Draft,
        FormVersionStatus.Published => FormVersionStatusDto.Published,
        _ => throw new NotSupportedException($"Unknown form version status '{status}'."),
    };

    private static FormPageDto ToDto(FormPage page) => new()
    {
        Id = page.Id,
        Title = page.Title,
        Questions = page.Questions.Select(QuestionMapper.ToDto).ToList(),
    };
}

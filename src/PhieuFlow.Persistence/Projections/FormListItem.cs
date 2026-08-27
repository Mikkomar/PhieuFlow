using PhieuFlow.Core.Entities;

namespace PhieuFlow.Persistence.Projections;

public class FormListItem
{
    public required Guid Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
    public required DateTimeOffset LastModifiedAt { get; set; }
    public string? LastModifiedBy { get; set; }
    public required int Revision { get; set; }
    public required int VersionNumber { get; set; }
    public required FormVersionStatus Status { get; set; }
    public required int PageCount { get; set; }
    public required int QuestionCount { get; set; }
}

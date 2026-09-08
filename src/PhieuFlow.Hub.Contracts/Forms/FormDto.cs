using PhieuFlow.Hub.Contracts.Validation;

namespace PhieuFlow.Hub.Contracts.Forms;

public class FormDto
{
    public required Guid Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
    public required DateTimeOffset LastModifiedAt { get; set; }
    public string? LastModifiedBy { get; set; }
    public required int Revision { get; set; }
    public required int VersionNumber { get; set; }
    public int? LatestPublishedVersionNumber { get; set; }
    public required FormVersionStatusDto Status { get; set; }
    public required List<FormPageDto> Pages { get; set; }

    /// <summary>Publish-blocking problems with the whole form. The Hub validator fills this list.</summary>
    public List<ValidationIssueDto> Issues { get; set; } = [];
}

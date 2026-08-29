using PhieuFlow.Core.Entities;

namespace PhieuFlow.FormBuilder.Models.Editing;

/// <summary>
/// The tree the builder edits. Every node carries an <see cref="Issues"/> collection the Hub
/// validator populates on a publish attempt; <see cref="HasIssues"/> is what blocks publish.
/// </summary>
public sealed class FormEditModel
{
    public Guid FormId { get; set; }
    public int VersionNumber { get; set; } = 1;
    public int Revision { get; set; } = 1;
    public FormVersionStatus Status { get; set; } = FormVersionStatus.Draft;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastModifiedAt { get; set; }
    public string? LastModifiedBy { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>The version currently live for respondents, if any — feeds the pre-publish version-summary line.</summary>
    public int? LiveVersionNumber { get; set; }

    public List<FormPageEditModel> Pages { get; set; } = [];
    public List<ValidationIssue> Issues { get; } = [];

    public bool HasIssues => Issues.Count > 0 || Pages.Any(p => p.HasIssues);
}

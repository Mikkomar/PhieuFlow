namespace PhieuFlow.Core.Entities;

public class FormVersion
{
    public required Guid Id { get; set; }
    public required Guid FormId { get; set; }
    public Form Form { get; set; } = null!;
    public int VersionNumber { get; set; } = 1;
    public required string Title { get; set; }
    public string? Description { get; set; }
    public int Revision { get; set; } = 1;
    public FormVersionStatus Status { get; set; } = FormVersionStatus.Draft;
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastModifiedAt { get; set; }
    public string? LastModifiedBy { get; set; }
    public ICollection<FormPage> Pages { get; set; } = new List<FormPage>();
}

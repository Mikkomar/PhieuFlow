namespace PhieuFlow.FormBuilder.Models;

public enum FormStatus
{
    Draft,
    Published,
}

public class FormSummary
{
    public required Guid Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public required FormStatus Status { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
    public required DateTimeOffset LastModifiedAt { get; set; }
    public required string LastModifiedBy { get; set; }
    public required int Revision { get; set; }
    public required int VersionNumber { get; set; }
    public int? LatestPublishedVersionNumber { get; set; }
    public DateTimeOffset? LatestPublishedAt { get; set; }
    public required int QuestionCount { get; set; }
    public required int PageCount { get; set; }
    public string? PublicUrl { get; set; }
}

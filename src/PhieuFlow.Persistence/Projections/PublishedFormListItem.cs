namespace PhieuFlow.Persistence.Projections;

public class PublishedFormListItem
{
    public required Guid Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public required int VersionNumber { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}

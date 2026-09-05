namespace PhieuFlow.Persistence.Projections;

public class PublishedFormBatchResult
{
    public required IReadOnlyList<PublishedFormListItem> Items { get; set; }
    public Guid? NextStartId { get; set; }
}

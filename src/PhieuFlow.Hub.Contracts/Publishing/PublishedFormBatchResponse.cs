namespace PhieuFlow.Hub.Contracts.Publishing;

public class PublishedFormBatchResponse
{
    public required IReadOnlyList<PublishedFormListItemDto> Items { get; set; }
    public Guid? NextStartId { get; set; }
}

namespace PhieuFlow.Hub.Contracts;

public class PublishedFormBatchResponse
{
    public required IReadOnlyList<PublishedFormListItemDto> Items { get; set; }
    public Guid? NextStartId { get; set; }
}

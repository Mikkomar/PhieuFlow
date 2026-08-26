namespace PhieuFlow.Hub.Contracts;

public class FormBatchResponse
{
    public required IReadOnlyList<FormListItemDto> Items { get; set; }
    public Guid? NextStartId { get; set; }
}

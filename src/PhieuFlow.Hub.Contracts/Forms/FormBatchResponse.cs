namespace PhieuFlow.Hub.Contracts.Forms;

public class FormBatchResponse
{
    public required IReadOnlyList<FormListItemDto> Items { get; set; }
    public Guid? NextStartId { get; set; }
}

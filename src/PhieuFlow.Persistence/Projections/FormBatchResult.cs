namespace PhieuFlow.Persistence.Projections;

public class FormBatchResult
{
    public required IReadOnlyList<FormListItem> Items { get; set; }
    public Guid? NextStartId { get; set; }
}

namespace PhieuFlow.Persistence.Projections;

public class SubmissionBatchResult
{
    public required IReadOnlyList<SubmissionListItem> Items { get; set; }
    public Guid? NextStartId { get; set; }
}

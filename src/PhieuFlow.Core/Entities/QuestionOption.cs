namespace PhieuFlow.Core.Entities;

public class QuestionOption
{
    public required Guid Id { get; set; }
    public required string Label { get; set; }
    public int Order { get; set; }
}

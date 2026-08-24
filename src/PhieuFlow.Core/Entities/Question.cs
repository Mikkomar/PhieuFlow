namespace PhieuFlow.Core.Entities;

public abstract class Question
{
    public required Guid Id { get; set; }
    public required string Text { get; set; }
    public bool IsRequired { get; set; }
}

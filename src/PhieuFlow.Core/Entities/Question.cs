namespace PhieuFlow.Core.Entities;

public abstract class Question
{
    public required Guid Id { get; set; }
    public required Guid FormPageId { get; set; }
    public FormPage FormPage { get; set; } = null!;
    public required string Text { get; set; }
    public bool IsRequired { get; set; }
    public int Order { get; set; }
}

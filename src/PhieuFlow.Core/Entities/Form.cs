namespace PhieuFlow.Core.Entities;

public class Form
{
    public required Guid Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public ICollection<FormPage> Pages { get; set; } = new List<FormPage>();
}

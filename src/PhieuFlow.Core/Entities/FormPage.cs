namespace PhieuFlow.Core.Entities;

public class FormPage
{
    public required Guid Id { get; set; }
    public required Guid FormId { get; set; }
    public Form Form { get; set; } = null!;
    public string? Title { get; set; }
    public int Order { get; set; }
    public ICollection<Question> Questions { get; set; } = new List<Question>();
}

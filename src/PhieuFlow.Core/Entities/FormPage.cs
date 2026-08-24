namespace PhieuFlow.Core.Entities;

public class FormPage
{
    public required Guid Id { get; set; }
    public string? Title { get; set; }
    public ICollection<Question> Questions { get; set; } = new List<Question>();
}

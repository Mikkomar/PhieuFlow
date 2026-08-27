namespace PhieuFlow.Core.Entities;

public class Form
{
    public required Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public ICollection<FormVersion> Versions { get; set; } = new List<FormVersion>();
}

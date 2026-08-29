namespace PhieuFlow.Hub.Contracts;

public class QuestionOptionDto
{
    public required Guid Id { get; set; }
    public required string Label { get; set; }
    public int Order { get; set; }

    /// <summary>Publish-blocking problems with this option. Populated by the Hub validator.</summary>
    public List<ValidationIssueDto> Issues { get; set; } = [];
}

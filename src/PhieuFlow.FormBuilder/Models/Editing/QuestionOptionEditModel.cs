namespace PhieuFlow.FormBuilder.Models.Editing;

public sealed class QuestionOptionEditModel : IHasIssues
{
    public required Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public int Order { get; set; }
    public List<ValidationIssue> Issues { get; } = [];
    public bool HasIssues => Issues.Count > 0;
}

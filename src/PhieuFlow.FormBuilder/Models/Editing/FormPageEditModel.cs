namespace PhieuFlow.FormBuilder.Models.Editing;

public sealed class FormPageEditModel
{
    public required Guid Id { get; set; }
    public string? Title { get; set; }
    public int Order { get; set; }
    public List<QuestionEditModel> Questions { get; set; } = [];
    public List<ValidationIssue> Issues { get; } = [];
    public bool HasIssues => Issues.Count > 0 || Questions.Any(q => q.HasIssues);
}

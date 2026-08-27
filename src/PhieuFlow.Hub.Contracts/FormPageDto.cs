namespace PhieuFlow.Hub.Contracts;

public class FormPageDto
{
    public required Guid Id { get; set; }
    public string? Title { get; set; }
    public required List<QuestionDto> Questions { get; set; }
}

namespace PhieuFlow.Core.Entities;

public abstract class ChoiceQuestion : Question
{
    public ICollection<QuestionOption> Options { get; set; } = new List<QuestionOption>();
}

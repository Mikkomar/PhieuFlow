namespace PhieuFlow.Core.Entities;

public class CheckBoxGroupQuestion : ChoiceQuestion
{
    public int? MinSelections { get; set; }
    public int? MaxSelections { get; set; }
}

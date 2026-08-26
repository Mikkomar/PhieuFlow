namespace PhieuFlow.Core.Entities;

public class CalendarQuestion : Question
{
    public DateOnly? MinDate { get; set; }
    public DateOnly? MaxDate { get; set; }
}

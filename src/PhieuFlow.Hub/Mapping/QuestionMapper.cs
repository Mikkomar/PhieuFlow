using PhieuFlow.Core.Entities;
using PhieuFlow.Hub.Contracts;

namespace PhieuFlow.Hub.Mapping;

/// <summary>
/// Polymorphic mapping between <see cref="Question"/> entities and their wire
/// <see cref="QuestionDto"/> counterparts. This is the one place that changes when a
/// question type is added on the Hub side.
/// </summary>
internal static class QuestionMapper
{
    public static QuestionDto ToDto(Question question) => question switch
    {
        TextAreaQuestion q => new TextAreaQuestionDto
        {
            Id = q.Id,
            Text = q.Text,
            IsRequired = q.IsRequired,
            MinLength = q.MinLength,
            MaxLength = q.MaxLength,
        },
        CheckboxQuestion q => new CheckboxQuestionDto
        {
            Id = q.Id,
            Text = q.Text,
            IsRequired = q.IsRequired,
            Label = q.Label,
        },
        DropDownQuestion q => new DropDownQuestionDto
        {
            Id = q.Id,
            Text = q.Text,
            IsRequired = q.IsRequired,
            Options = OptionsToDto(q.Options),
        },
        RadioButtonQuestion q => new RadioButtonQuestionDto
        {
            Id = q.Id,
            Text = q.Text,
            IsRequired = q.IsRequired,
            Options = OptionsToDto(q.Options),
        },
        CheckBoxGroupQuestion q => new CheckBoxGroupQuestionDto
        {
            Id = q.Id,
            Text = q.Text,
            IsRequired = q.IsRequired,
            Options = OptionsToDto(q.Options),
            MinSelections = q.MinSelections,
            MaxSelections = q.MaxSelections,
        },
        NumberQuestion q => new NumberQuestionDto
        {
            Id = q.Id,
            Text = q.Text,
            IsRequired = q.IsRequired,
            Min = q.Min,
            Max = q.Max,
        },
        CalendarQuestion q => new CalendarQuestionDto
        {
            Id = q.Id,
            Text = q.Text,
            IsRequired = q.IsRequired,
            MinDate = q.MinDate,
            MaxDate = q.MaxDate,
        },
        _ => throw new NotSupportedException($"Unknown question type '{question.GetType().Name}'."),
    };

    public static Question ToEntity(QuestionDto dto, Guid formPageId, int order) => dto switch
    {
        TextAreaQuestionDto q => new TextAreaQuestion
        {
            Id = q.Id,
            FormPageId = formPageId,
            Text = q.Text,
            IsRequired = q.IsRequired,
            Order = order,
            MinLength = q.MinLength,
            MaxLength = q.MaxLength,
        },
        CheckboxQuestionDto q => new CheckboxQuestion
        {
            Id = q.Id,
            FormPageId = formPageId,
            Text = q.Text,
            IsRequired = q.IsRequired,
            Order = order,
            Label = q.Label,
        },
        DropDownQuestionDto q => new DropDownQuestion
        {
            Id = q.Id,
            FormPageId = formPageId,
            Text = q.Text,
            IsRequired = q.IsRequired,
            Order = order,
            Options = OptionsToEntity(q.Options),
        },
        RadioButtonQuestionDto q => new RadioButtonQuestion
        {
            Id = q.Id,
            FormPageId = formPageId,
            Text = q.Text,
            IsRequired = q.IsRequired,
            Order = order,
            Options = OptionsToEntity(q.Options),
        },
        CheckBoxGroupQuestionDto q => new CheckBoxGroupQuestion
        {
            Id = q.Id,
            FormPageId = formPageId,
            Text = q.Text,
            IsRequired = q.IsRequired,
            Order = order,
            Options = OptionsToEntity(q.Options),
            MinSelections = q.MinSelections,
            MaxSelections = q.MaxSelections,
        },
        NumberQuestionDto q => new NumberQuestion
        {
            Id = q.Id,
            FormPageId = formPageId,
            Text = q.Text,
            IsRequired = q.IsRequired,
            Order = order,
            Min = q.Min,
            Max = q.Max,
        },
        CalendarQuestionDto q => new CalendarQuestion
        {
            Id = q.Id,
            FormPageId = formPageId,
            Text = q.Text,
            IsRequired = q.IsRequired,
            Order = order,
            MinDate = q.MinDate,
            MaxDate = q.MaxDate,
        },
        _ => throw new NotSupportedException($"Unknown question type '{dto.GetType().Name}'."),
    };

    private static List<QuestionOptionDto> OptionsToDto(IEnumerable<QuestionOption> options) => options
        .OrderBy(o => o.Order)
        .Select(o => new QuestionOptionDto { Id = o.Id, Label = o.Label, Order = o.Order })
        .ToList();

    // Option order is authoritative from the incoming list position, mirroring how pages
    // and questions get their Order.
    private static List<QuestionOption> OptionsToEntity(IEnumerable<QuestionOptionDto> options) => options
        .Select((o, index) => new QuestionOption { Id = o.Id, Label = o.Label, Order = index })
        .ToList();
}

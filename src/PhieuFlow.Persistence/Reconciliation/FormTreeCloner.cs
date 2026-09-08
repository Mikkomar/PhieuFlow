using PhieuFlow.Core.Entities;

namespace PhieuFlow.Persistence.Reconciliation;

/// <inheritdoc />
public sealed class FormTreeCloner : IFormTreeCloner
{
    public FormPage ClonePageWithFreshIds(FormPage source, Guid newVersionId)
    {
        var newPageId = Guid.NewGuid();
        return new FormPage
        {
            Id = newPageId,
            FormVersionId = newVersionId,
            Title = source.Title,
            Order = source.Order,
            Questions = source.Questions.Select(q => CloneQuestionWithFreshIds(q, newPageId)).ToList(),
        };
    }

    private static Question CloneQuestionWithFreshIds(Question source, Guid newPageId)
    {
        var newQuestionId = Guid.NewGuid();
        return source switch
        {
            TextAreaQuestion q => new TextAreaQuestion
            {
                Id = newQuestionId, FormPageId = newPageId, Text = q.Text, IsRequired = q.IsRequired, Order = q.Order,
                MinLength = q.MinLength, MaxLength = q.MaxLength,
            },
            CheckboxQuestion q => new CheckboxQuestion
            {
                Id = newQuestionId, FormPageId = newPageId, Text = q.Text, IsRequired = q.IsRequired, Order = q.Order,
                Label = q.Label,
            },
            DropDownQuestion q => new DropDownQuestion
            {
                Id = newQuestionId, FormPageId = newPageId, Text = q.Text, IsRequired = q.IsRequired, Order = q.Order,
                Options = CloneOptionsWithFreshIds(q.Options),
            },
            RadioButtonQuestion q => new RadioButtonQuestion
            {
                Id = newQuestionId, FormPageId = newPageId, Text = q.Text, IsRequired = q.IsRequired, Order = q.Order,
                Options = CloneOptionsWithFreshIds(q.Options),
            },
            CheckBoxGroupQuestion q => new CheckBoxGroupQuestion
            {
                Id = newQuestionId, FormPageId = newPageId, Text = q.Text, IsRequired = q.IsRequired, Order = q.Order,
                Options = CloneOptionsWithFreshIds(q.Options),
                MinSelections = q.MinSelections, MaxSelections = q.MaxSelections,
            },
            NumberQuestion q => new NumberQuestion
            {
                Id = newQuestionId, FormPageId = newPageId, Text = q.Text, IsRequired = q.IsRequired, Order = q.Order,
                Min = q.Min, Max = q.Max,
            },
            CalendarQuestion q => new CalendarQuestion
            {
                Id = newQuestionId, FormPageId = newPageId, Text = q.Text, IsRequired = q.IsRequired, Order = q.Order,
                MinDate = q.MinDate, MaxDate = q.MaxDate,
            },
            _ => throw new NotSupportedException($"Unknown question type '{source.GetType().Name}'."),
        };
    }

    private static List<QuestionOption> CloneOptionsWithFreshIds(IEnumerable<QuestionOption> options) => options
        .Select(o => new QuestionOption { Id = Guid.NewGuid(), Label = o.Label, Order = o.Order })
        .ToList();
}

using PhieuFlow.Core.Entities;

namespace PhieuFlow.Tests.Unit;

/// <summary>
/// Terse <see cref="FormVersion"/> entity-tree builders for <see cref="FormVersionReconcilerTests"/>.
/// <see cref="DeepCopy"/> makes an id-preserving clone, like the tree a client PUTs back
/// after an edit.
/// </summary>
internal static class EntityTreeBuilder
{
    public static FormVersion Version(FormVersionStatus status, params FormPage[] pages)
    {
        var versionId = Guid.NewGuid();
        foreach (var page in pages)
        {
            page.FormVersionId = versionId;
        }

        return new FormVersion
        {
            Id = versionId,
            FormId = Guid.NewGuid(),
            VersionNumber = 1,
            Revision = 1,
            Status = status,
            Title = "Untitled",
            CreatedAt = DateTimeOffset.UtcNow,
            LastModifiedAt = DateTimeOffset.UtcNow,
            Pages = pages.ToList(),
        };
    }

    public static FormPage Page(int order, string? title, params Question[] questions)
    {
        var pageId = Guid.NewGuid();
        foreach (var (question, index) in questions.Select((q, i) => (q, i)))
        {
            question.FormPageId = pageId;
            if (question.Order == 0)
            {
                question.Order = index;
            }
        }

        return new FormPage
        {
            Id = pageId,
            FormVersionId = Guid.Empty,
            Title = title,
            Order = order,
            Questions = questions.ToList(),
        };
    }

    public static TextAreaQuestion TextArea(
        string text, bool required = false, int order = 0, int? minLength = null, int? maxLength = null) => new()
    {
        Id = Guid.NewGuid(), FormPageId = Guid.Empty, Text = text, IsRequired = required, Order = order,
        MinLength = minLength, MaxLength = maxLength,
    };

    public static CheckboxQuestion Checkbox(string text, string label, bool required = false, int order = 0) => new()
    {
        Id = Guid.NewGuid(), FormPageId = Guid.Empty, Text = text, IsRequired = required, Order = order,
        Label = label,
    };

    public static NumberQuestion Number(
        string text, decimal? min = null, decimal? max = null, bool required = false, int order = 0) => new()
    {
        Id = Guid.NewGuid(), FormPageId = Guid.Empty, Text = text, IsRequired = required, Order = order,
        Min = min, Max = max,
    };

    public static CalendarQuestion Calendar(
        string text, DateOnly? minDate = null, DateOnly? maxDate = null, bool required = false, int order = 0) => new()
    {
        Id = Guid.NewGuid(), FormPageId = Guid.Empty, Text = text, IsRequired = required, Order = order,
        MinDate = minDate, MaxDate = maxDate,
    };

    public static DropDownQuestion DropDown(string text, params string[] optionLabels) => new()
    {
        Id = Guid.NewGuid(), FormPageId = Guid.Empty, Text = text, IsRequired = false, Order = 0,
        Options = Options(optionLabels),
    };

    public static RadioButtonQuestion Radio(string text, params string[] optionLabels) => new()
    {
        Id = Guid.NewGuid(), FormPageId = Guid.Empty, Text = text, IsRequired = false, Order = 0,
        Options = Options(optionLabels),
    };

    public static CheckBoxGroupQuestion CheckBoxGroup(
        string text, int? minSelections, int? maxSelections, params string[] optionLabels) => new()
    {
        Id = Guid.NewGuid(), FormPageId = Guid.Empty, Text = text, IsRequired = false, Order = 0,
        MinSelections = minSelections, MaxSelections = maxSelections,
        Options = Options(optionLabels),
    };

    public static List<QuestionOption> Options(params string[] labels) => labels
        .Select((label, i) => new QuestionOption { Id = Guid.NewGuid(), Label = label, Order = i })
        .ToList();

    /// <summary>Id-preserving deep copy: the shape a client PUTs back after an edit.</summary>
    public static FormVersion DeepCopy(this FormVersion source) => new()
    {
        Id = source.Id,
        FormId = source.FormId,
        VersionNumber = source.VersionNumber,
        Revision = source.Revision,
        Status = source.Status,
        Title = source.Title,
        Description = source.Description,
        LastModifiedBy = source.LastModifiedBy,
        CreatedAt = source.CreatedAt,
        LastModifiedAt = source.LastModifiedAt,
        PublishedAt = source.PublishedAt,
        Pages = source.Pages.Select(CopyPage).ToList(),
    };

    private static FormPage CopyPage(FormPage source) => new()
    {
        Id = source.Id,
        FormVersionId = source.FormVersionId,
        Title = source.Title,
        Order = source.Order,
        Questions = source.Questions.Select(CopyQuestion).ToList(),
    };

    private static Question CopyQuestion(Question source) => source switch
    {
        TextAreaQuestion q => new TextAreaQuestion
        {
            Id = q.Id, FormPageId = q.FormPageId, Text = q.Text, IsRequired = q.IsRequired, Order = q.Order,
            MinLength = q.MinLength, MaxLength = q.MaxLength,
        },
        CheckboxQuestion q => new CheckboxQuestion
        {
            Id = q.Id, FormPageId = q.FormPageId, Text = q.Text, IsRequired = q.IsRequired, Order = q.Order,
            Label = q.Label,
        },
        NumberQuestion q => new NumberQuestion
        {
            Id = q.Id, FormPageId = q.FormPageId, Text = q.Text, IsRequired = q.IsRequired, Order = q.Order,
            Min = q.Min, Max = q.Max,
        },
        CalendarQuestion q => new CalendarQuestion
        {
            Id = q.Id, FormPageId = q.FormPageId, Text = q.Text, IsRequired = q.IsRequired, Order = q.Order,
            MinDate = q.MinDate, MaxDate = q.MaxDate,
        },
        CheckBoxGroupQuestion q => new CheckBoxGroupQuestion
        {
            Id = q.Id, FormPageId = q.FormPageId, Text = q.Text, IsRequired = q.IsRequired, Order = q.Order,
            MinSelections = q.MinSelections, MaxSelections = q.MaxSelections,
            Options = q.Options.Select(CopyOption).ToList(),
        },
        DropDownQuestion q => new DropDownQuestion
        {
            Id = q.Id, FormPageId = q.FormPageId, Text = q.Text, IsRequired = q.IsRequired, Order = q.Order,
            Options = q.Options.Select(CopyOption).ToList(),
        },
        RadioButtonQuestion q => new RadioButtonQuestion
        {
            Id = q.Id, FormPageId = q.FormPageId, Text = q.Text, IsRequired = q.IsRequired, Order = q.Order,
            Options = q.Options.Select(CopyOption).ToList(),
        },
        _ => throw new NotSupportedException($"Unhandled question type '{source.GetType().Name}'."),
    };

    private static QuestionOption CopyOption(QuestionOption source) => new()
    {
        Id = source.Id,
        Label = source.Label,
        Order = source.Order,
    };
}

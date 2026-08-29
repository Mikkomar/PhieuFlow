using PhieuFlow.Hub.Contracts;

namespace PhieuFlow.Tests.Unit;

/// <summary>Terse <see cref="FormDto"/> tree builders for the validator tests.</summary>
internal static class TreeBuilder
{
    public static FormDto Form(string title, params FormPageDto[] pages) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        CreatedAt = DateTimeOffset.UtcNow,
        LastModifiedAt = DateTimeOffset.UtcNow,
        Revision = 1,
        VersionNumber = 1,
        Status = FormVersionStatusDto.Draft,
        Pages = pages.ToList(),
    };

    public static FormPageDto Page(string? title, params QuestionDto[] questions) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        Questions = questions.ToList(),
    };

    public static TextAreaQuestionDto TextArea(string text) => new()
    {
        Id = Guid.NewGuid(),
        Text = text,
        IsRequired = false,
    };

    public static NumberQuestionDto Number(string text, decimal? min = null, decimal? max = null) => new()
    {
        Id = Guid.NewGuid(),
        Text = text,
        IsRequired = false,
        Min = min,
        Max = max,
    };

    public static DropDownQuestionDto DropDown(string text, params string[] optionLabels) => new()
    {
        Id = Guid.NewGuid(),
        Text = text,
        IsRequired = false,
        Options = Options(optionLabels),
    };

    public static CheckBoxGroupQuestionDto CheckBoxGroup(string text, int? minSelections, int? maxSelections, params string[] optionLabels) => new()
    {
        Id = Guid.NewGuid(),
        Text = text,
        IsRequired = false,
        MinSelections = minSelections,
        MaxSelections = maxSelections,
        Options = Options(optionLabels),
    };

    public static List<QuestionOptionDto> Options(params string[] labels) => labels
        .Select((label, i) => new QuestionOptionDto { Id = Guid.NewGuid(), Label = label, Order = i })
        .ToList();

    public static IEnumerable<ValidationIssueDto> AllIssues(this FormDto form)
    {
        foreach (var issue in form.Issues)
        {
            yield return issue;
        }

        foreach (var page in form.Pages)
        {
            foreach (var issue in page.Issues)
            {
                yield return issue;
            }

            foreach (var question in page.Questions)
            {
                foreach (var issue in question.Issues)
                {
                    yield return issue;
                }

                if (question is ChoiceQuestionDto choice)
                {
                    foreach (var option in choice.Options)
                    {
                        foreach (var issue in option.Issues)
                        {
                            yield return issue;
                        }
                    }
                }
            }
        }
    }
}

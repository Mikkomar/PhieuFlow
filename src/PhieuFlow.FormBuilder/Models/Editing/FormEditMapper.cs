using PhieuFlow.Core.Entities;
using PhieuFlow.Hub.Contracts;

namespace PhieuFlow.FormBuilder.Models.Editing;

/// <summary>
/// Maps between the builder's <see cref="FormEditModel"/> tree and the wire <see cref="FormDto"/>.
/// Both directions carry the per-node validation <c>Issues</c>, so an annotated response maps
/// straight back onto a fresh edit tree, and <see cref="ApplyIssues"/> copies issues onto an
/// existing tree without disturbing in-flight edits.
/// </summary>
public static class FormEditMapper
{
    public static FormDto ToDto(FormEditModel form) => new()
    {
        Id = form.FormId,
        Title = form.Title,
        Description = form.Description,
        CreatedAt = form.CreatedAt,
        LastModifiedAt = form.LastModifiedAt,
        LastModifiedBy = form.LastModifiedBy,
        Revision = form.Revision,
        VersionNumber = form.VersionNumber,
        LatestPublishedVersionNumber = form.LiveVersionNumber,
        Status = ToDto(form.Status),
        Pages = form.Pages.Select(ToDto).ToList(),
        Issues = form.Issues.Select(ToDto).ToList(),
    };

    public static FormEditModel ToEditModel(FormDto dto)
    {
        var form = new FormEditModel
        {
            FormId = dto.Id,
            Title = dto.Title,
            Description = dto.Description,
            CreatedAt = dto.CreatedAt,
            LastModifiedAt = dto.LastModifiedAt,
            LastModifiedBy = dto.LastModifiedBy,
            Revision = dto.Revision,
            VersionNumber = dto.VersionNumber,
            LiveVersionNumber = dto.LatestPublishedVersionNumber,
            Status = ToEntity(dto.Status),
            Pages = dto.Pages.Select((p, i) => ToEditModel(p, i)).ToList(),
        };

        form.Issues.AddRange(dto.Issues.Select(ToIssue));
        return form;
    }

    /// <summary>Replaces every node's <c>Issues</c> in <paramref name="target"/> with those from the matching node in <paramref name="annotated"/>.</summary>
    public static void ApplyIssues(FormEditModel target, FormDto annotated)
    {
        Replace(target.Issues, annotated.Issues);

        var pagesById = target.Pages.ToDictionary(p => p.Id);
        foreach (var pageDto in annotated.Pages)
        {
            if (!pagesById.TryGetValue(pageDto.Id, out var page))
            {
                continue;
            }

            Replace(page.Issues, pageDto.Issues);

            var questionsById = page.Questions.ToDictionary(q => q.Id);
            foreach (var questionDto in pageDto.Questions)
            {
                if (!questionsById.TryGetValue(questionDto.Id, out var question))
                {
                    continue;
                }

                Replace(question.Issues, questionDto.Issues);

                if (question is ChoiceQuestionEditModel choice && questionDto is ChoiceQuestionDto choiceDto)
                {
                    var optionsById = choice.Options.ToDictionary(o => o.Id);
                    foreach (var optionDto in choiceDto.Options)
                    {
                        if (optionsById.TryGetValue(optionDto.Id, out var option))
                        {
                            Replace(option.Issues, optionDto.Issues);
                        }
                    }
                }
            }
        }
    }

    private static void Replace(List<ValidationIssue> target, List<ValidationIssueDto> source)
    {
        target.Clear();
        target.AddRange(source.Select(ToIssue));
    }

    private static ValidationIssue ToIssue(ValidationIssueDto dto) => new(dto.Message, dto.Field);

    private static ValidationIssueDto ToDto(ValidationIssue issue) => new() { Message = issue.Message, Field = issue.Field };

    private static FormPageDto ToDto(FormPageEditModel page) => new()
    {
        Id = page.Id,
        Title = page.Title,
        Questions = page.Questions.Select(ToDto).ToList(),
        Issues = page.Issues.Select(ToDto).ToList(),
    };

    private static QuestionDto ToDto(QuestionEditModel question)
    {
        QuestionDto dto = question switch
        {
            TextAreaQuestionEditModel q => new TextAreaQuestionDto
            {
                Id = q.Id, Text = q.Text, IsRequired = q.IsRequired, MinLength = q.MinLength, MaxLength = q.MaxLength,
            },
            CheckboxQuestionEditModel q => new CheckboxQuestionDto
            {
                Id = q.Id, Text = q.Text, IsRequired = q.IsRequired, Label = q.Label,
            },
            DropDownQuestionEditModel q => new DropDownQuestionDto
            {
                Id = q.Id, Text = q.Text, IsRequired = q.IsRequired, Options = ToDto(q.Options),
            },
            RadioButtonQuestionEditModel q => new RadioButtonQuestionDto
            {
                Id = q.Id, Text = q.Text, IsRequired = q.IsRequired, Options = ToDto(q.Options),
            },
            CheckBoxGroupQuestionEditModel q => new CheckBoxGroupQuestionDto
            {
                Id = q.Id, Text = q.Text, IsRequired = q.IsRequired, Options = ToDto(q.Options),
                MinSelections = q.MinSelections, MaxSelections = q.MaxSelections,
            },
            NumberQuestionEditModel q => new NumberQuestionDto
            {
                Id = q.Id, Text = q.Text, IsRequired = q.IsRequired, Min = q.Min, Max = q.Max,
            },
            CalendarQuestionEditModel q => new CalendarQuestionDto
            {
                Id = q.Id, Text = q.Text, IsRequired = q.IsRequired, MinDate = q.MinDate, MaxDate = q.MaxDate,
            },
            _ => throw new NotSupportedException($"Unknown question edit model '{question.GetType().Name}'."),
        };

        dto.Issues = question.Issues.Select(ToDto).ToList();
        return dto;
    }

    private static List<QuestionOptionDto> ToDto(IEnumerable<QuestionOptionEditModel> options) => options
        .OrderBy(o => o.Order)
        .Select((o, index) => new QuestionOptionDto
        {
            Id = o.Id,
            Label = o.Label,
            Order = index,
            Issues = o.Issues.Select(ToDto).ToList(),
        })
        .ToList();

    private static FormPageEditModel ToEditModel(FormPageDto dto, int order)
    {
        var page = new FormPageEditModel
        {
            Id = dto.Id,
            Title = dto.Title,
            Order = order,
            Questions = dto.Questions.Select((q, i) => ToEditModel(q, i)).ToList(),
        };

        page.Issues.AddRange(dto.Issues.Select(ToIssue));
        return page;
    }

    private static QuestionEditModel ToEditModel(QuestionDto dto, int order)
    {
        QuestionEditModel model = dto switch
        {
            TextAreaQuestionDto q => new TextAreaQuestionEditModel
            {
                Id = q.Id, Text = q.Text, IsRequired = q.IsRequired, MinLength = q.MinLength, MaxLength = q.MaxLength,
            },
            CheckboxQuestionDto q => new CheckboxQuestionEditModel
            {
                Id = q.Id, Text = q.Text, IsRequired = q.IsRequired, Label = q.Label,
            },
            DropDownQuestionDto q => new DropDownQuestionEditModel
            {
                Id = q.Id, Text = q.Text, IsRequired = q.IsRequired, Options = ToEditModel(q.Options),
            },
            RadioButtonQuestionDto q => new RadioButtonQuestionEditModel
            {
                Id = q.Id, Text = q.Text, IsRequired = q.IsRequired, Options = ToEditModel(q.Options),
            },
            CheckBoxGroupQuestionDto q => new CheckBoxGroupQuestionEditModel
            {
                Id = q.Id, Text = q.Text, IsRequired = q.IsRequired, Options = ToEditModel(q.Options),
                MinSelections = q.MinSelections, MaxSelections = q.MaxSelections,
            },
            NumberQuestionDto q => new NumberQuestionEditModel
            {
                Id = q.Id, Text = q.Text, IsRequired = q.IsRequired, Min = q.Min, Max = q.Max,
            },
            CalendarQuestionDto q => new CalendarQuestionEditModel
            {
                Id = q.Id, Text = q.Text, IsRequired = q.IsRequired, MinDate = q.MinDate, MaxDate = q.MaxDate,
            },
            _ => throw new NotSupportedException($"Unknown question dto '{dto.GetType().Name}'."),
        };

        model.Order = order;
        model.Issues.AddRange(dto.Issues.Select(ToIssue));
        return model;
    }

    private static List<QuestionOptionEditModel> ToEditModel(IEnumerable<QuestionOptionDto> options) => options
        .OrderBy(o => o.Order)
        .Select((o, index) =>
        {
            var model = new QuestionOptionEditModel { Id = o.Id, Label = o.Label, Order = index };
            model.Issues.AddRange(o.Issues.Select(ToIssue));
            return model;
        })
        .ToList();

    private static FormVersionStatusDto ToDto(FormVersionStatus status) => status switch
    {
        FormVersionStatus.Published => FormVersionStatusDto.Published,
        _ => FormVersionStatusDto.Draft,
    };

    private static FormVersionStatus ToEntity(FormVersionStatusDto status) => status switch
    {
        FormVersionStatusDto.Published => FormVersionStatus.Published,
        _ => FormVersionStatus.Draft,
    };
}

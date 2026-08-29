using PhieuFlow.Hub.Contracts;

namespace PhieuFlow.Hub.Validation;

/// <summary>
/// The single pre-publish gate. Walks the form tree and, for every rule breach, appends a
/// <see cref="ValidationIssueDto"/> to the offending node's <c>Issues</c> list. A tree with
/// any issue must not be published.
/// </summary>
/// <remarks>
/// This is the only validator. The builder does not re-implement these rules client-side; it
/// renders whatever issues the Hub hangs on the returned tree.
/// </remarks>
public interface IFormPublishValidator
{
    /// <summary>Populates <c>Issues</c> across <paramref name="form"/>. Returns true when the form is publishable.</summary>
    bool Validate(FormDto form);
}

public sealed class FormPublishValidator : IFormPublishValidator
{
    public bool Validate(FormDto form)
    {
        Clear(form);

        var clean = true;

        if (string.IsNullOrWhiteSpace(form.Title))
        {
            form.Issues.Add(new ValidationIssueDto
            {
                Message = "The form has no title.",
                Field = ValidationField.Title,
            });
            clean = false;
        }

        var singlePage = form.Pages.Count == 1;

        for (var pageIndex = 0; pageIndex < form.Pages.Count; pageIndex++)
        {
            var page = form.Pages[pageIndex];
            var pageNumber = pageIndex + 1;
            var pageName = string.IsNullOrWhiteSpace(page.Title) ? $"Page {pageNumber}" : $"Page {pageNumber} “{page.Title}”";

            if (page.Questions.Count == 0 && singlePage)
            {
                page.Issues.Add(new ValidationIssueDto
                {
                    Message = $"{pageName} has no questions.",
                    Field = ValidationField.None,
                });
                clean = false;
            }

            for (var questionIndex = 0; questionIndex < page.Questions.Count; questionIndex++)
            {
                clean &= ValidateQuestion(page.Questions[questionIndex], questionIndex + 1);
            }
        }

        return clean;
    }

    private static bool ValidateQuestion(QuestionDto question, int questionNumber)
    {
        var clean = true;

        if (string.IsNullOrWhiteSpace(question.Text))
        {
            question.Issues.Add(new ValidationIssueDto
            {
                Message = $"Question {questionNumber} has no text.",
                Field = ValidationField.Text,
            });
            clean = false;
        }

        if (question is ChoiceQuestionDto choice)
        {
            clean &= ValidateChoice(choice, questionNumber);
        }

        var minGreaterThanMax = question switch
        {
            NumberQuestionDto n => ExceedsMax((n.Min, n.Max)),
            CalendarQuestionDto c => ExceedsMax((c.MinDate, c.MaxDate)),
            _ => false,
        };

        if (minGreaterThanMax)
        {
            question.Issues.Add(new ValidationIssueDto
            {
                Message = $"Question {questionNumber}: the minimum is greater than the maximum.",
                Field = ValidationField.Min,
            });
            clean = false;
        }

        if (question is CheckBoxGroupQuestionDto group)
        {
            if (ExceedsMax((group.MinSelections, group.MaxSelections)))
            {
                question.Issues.Add(new ValidationIssueDto
                {
                    Message = $"Question {questionNumber}: min selections ({group.MinSelections}) is greater than max selections ({group.MaxSelections}).",
                    Field = ValidationField.Min,
                });
                clean = false;
            }

            if (group.MinSelections is { } min && min > group.Options.Count)
            {
                question.Issues.Add(new ValidationIssueDto
                {
                    Message = $"Question {questionNumber}: min selections ({min}) is greater than the number of options ({group.Options.Count}).",
                    Field = ValidationField.MinSelections,
                });
                clean = false;
            }
        }

        return clean;
    }

    private static bool ValidateChoice(ChoiceQuestionDto choice, int questionNumber)
    {
        var clean = true;

        if (choice.Options.Count == 0)
        {
            choice.Issues.Add(new ValidationIssueDto
            {
                Message = $"Question {questionNumber} has no options.",
                Field = ValidationField.Options,
            });
            return false;
        }

        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < choice.Options.Count; i++)
        {
            var option = choice.Options[i];
            var optionNumber = i + 1;
            var label = option.Label?.Trim() ?? string.Empty;

            if (label.Length == 0)
            {
                option.Issues.Add(new ValidationIssueDto
                {
                    Message = $"Question {questionNumber}, option {optionNumber} has no label.",
                    Field = ValidationField.Label,
                });
                clean = false;
                continue;
            }

            if (seen.TryGetValue(label, out var firstNumber))
            {
                option.Issues.Add(new ValidationIssueDto
                {
                    Message = $"Question {questionNumber}, option {optionNumber} duplicates option {firstNumber}.",
                    Field = ValidationField.Label,
                });
                clean = false;
            }
            else
            {
                seen[label] = optionNumber;
            }
        }

        return clean;
    }

    private static bool ExceedsMax<T>((T? Min, T? Max) bounds) where T : struct, IComparable<T> =>
        bounds is { Min: { } min, Max: { } max } && min.CompareTo(max) > 0;

    private static void Clear(FormDto form)
    {
        form.Issues.Clear();
        foreach (var page in form.Pages)
        {
            page.Issues.Clear();
            foreach (var question in page.Questions)
            {
                question.Issues.Clear();
                if (question is ChoiceQuestionDto choice)
                {
                    foreach (var option in choice.Options)
                    {
                        option.Issues.Clear();
                    }
                }
            }
        }
    }
}

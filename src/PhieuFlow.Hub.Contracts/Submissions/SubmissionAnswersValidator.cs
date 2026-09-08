using System.Globalization;
using PhieuFlow.Hub.Contracts.Forms;
using PhieuFlow.Hub.Contracts.Publishing;

namespace PhieuFlow.Hub.Contracts.Submissions;

/// <summary>
/// Validates a set of submitted answers against a published form. Shared by the FormFiller
/// (which blocks a submission before publishing it, because the RabbitMQ hop is one-way —
/// ADR 0009) and the Hub consumer (which re-checks on the way in and dead-letters anything
/// that does not pass). One implementation so the two can never drift.
/// </summary>
/// <remarks>
/// Rules: <c>IsRequired</c>, number Min/Max, date Min/Max, text Min/Max length, checkbox-group
/// Min/Max selections — bounds only bite when a value is present, so an optional blank answer
/// passes. Plus structural integrity the client's UI cannot violate but a stale or crafted
/// message can: the answer names a question on the form, its type matches that question, and a
/// chosen option belongs to it.
/// </remarks>
public sealed class SubmissionAnswersValidator
{
    private const string RequiredMessage = "An answer is required.";

    /// <returns>
    /// One message per offending question, keyed by question id. An empty map means the
    /// answers are ready to submit.
    /// </returns>
    public IReadOnlyDictionary<Guid, string> Validate(
        PublishedFormDto form,
        IReadOnlyList<SubmissionAnswerDto> answers)
    {
        var errors = new Dictionary<Guid, string>();
        var questionsById = form.Pages
            .SelectMany(page => page.Questions)
            .ToDictionary(question => question.Id);

        // Structural pass — first error per question wins, and it wins over any constraint
        // error found below (TryAdd never overwrites).
        foreach (var answer in answers)
        {
            if (!questionsById.TryGetValue(answer.QuestionId, out var question))
            {
                errors.TryAdd(answer.QuestionId, "This answer does not correspond to a question on the form.");
                continue;
            }

            if (!TypeMatches(question, answer))
            {
                errors.TryAdd(answer.QuestionId, "The answer type does not match the question.");
                continue;
            }

            if (answer is OptionAnswerDto option
                && question is ChoiceQuestionDto choice
                && choice.Options.All(o => o.Id != option.OptionId))
            {
                errors.TryAdd(answer.QuestionId, "The selected option is not offered by this question.");
            }
        }

        // Constraint pass — one lookup of this question's answers, then the type-specific rule.
        foreach (var question in questionsById.Values)
        {
            var given = answers.Where(a => a.QuestionId == question.Id).ToList();
            var message = Check(question, given);
            if (message is not null)
            {
                errors.TryAdd(question.Id, message);
            }
        }

        return errors;
    }

    private static bool TypeMatches(QuestionDto question, SubmissionAnswerDto answer) => answer switch
    {
        ValueAnswerDto => question is TextAreaQuestionDto or NumberQuestionDto or CalendarQuestionDto,
        BooleanAnswerDto => question is CheckboxQuestionDto,
        OptionAnswerDto => question is ChoiceQuestionDto,
        _ => false,
    };

    private static string? Check(QuestionDto question, List<SubmissionAnswerDto> given) => question switch
    {
        TextAreaQuestionDto q => CheckText(q, Value(given)),
        NumberQuestionDto q => CheckNumber(q, Value(given)),
        CalendarQuestionDto q => CheckDate(q, Value(given)),
        CheckboxQuestionDto q => q.IsRequired && given.OfType<BooleanAnswerDto>().FirstOrDefault()?.Checked != true
            ? RequiredMessage
            : null,
        CheckBoxGroupQuestionDto q => CheckGroup(q, given.OfType<OptionAnswerDto>().Select(o => o.OptionId).ToList()),
        ChoiceQuestionDto q => CheckSingleChoice(q, given.OfType<OptionAnswerDto>().Count()),
        _ => null,
    };

    private static string? Value(List<SubmissionAnswerDto> given) =>
        given.OfType<ValueAnswerDto>().FirstOrDefault()?.Value;

    private static string? CheckText(TextAreaQuestionDto question, string? value)
    {
        var text = value ?? string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            return question.IsRequired ? RequiredMessage : null;
        }

        var tooShort = question.MinLength is { } lo && text.Length < lo;
        var tooLong = question.MaxLength is { } hi && text.Length > hi;

        if (!tooShort && !tooLong)
        {
            return null;
        }

        return (question.MinLength, question.MaxLength) switch
        {
            ({ } min, { } max) => $"Enter between {min} and {max} characters.",
            ({ } min, null) => $"Enter at least {min} characters.",
            (null, { } max) => $"Enter at most {max} characters.",
            _ => null,
        };
    }

    private static string? CheckNumber(NumberQuestionDto question, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return question.IsRequired ? RequiredMessage : null;
        }

        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
        {
            return "Enter a valid number.";
        }

        var belowMin = question.Min is { } min && number < min;
        var aboveMax = question.Max is { } max && number > max;

        return belowMin || aboveMax ? NumberBounds(question.Min, question.Max) : null;
    }

    private static string? CheckDate(CalendarQuestionDto question, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return question.IsRequired ? RequiredMessage : null;
        }

        if (!DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return "Enter a valid date.";
        }

        var belowMin = question.MinDate is { } min && date < min;
        var aboveMax = question.MaxDate is { } max && date > max;

        return belowMin || aboveMax ? DateBounds(question.MinDate, question.MaxDate) : null;
    }

    private static string? CheckGroup(CheckBoxGroupQuestionDto question, List<Guid> selected)
    {
        if (selected.Count != selected.Distinct().Count())
        {
            return "Each option can only be chosen once.";
        }

        var count = selected.Count;

        if (count == 0)
        {
            return question.IsRequired ? RequiredMessage : null;
        }

        var belowMin = question.MinSelections is { } lo && count < lo;
        var aboveMax = question.MaxSelections is { } hi && count > hi;

        if (!belowMin && !aboveMax)
        {
            return null;
        }

        return (question.MinSelections, question.MaxSelections) switch
        {
            ({ } min, { } max) => $"Choose between {min} and {max} options.",
            ({ } min, null) => $"Choose at least {min}.",
            (null, { } max) => $"Choose at most {max}.",
            _ => RequiredMessage,
        };
    }

    private static string? CheckSingleChoice(ChoiceQuestionDto question, int optionCount)
    {
        if (optionCount == 0)
        {
            return question.IsRequired ? "Select an option." : null;
        }

        return optionCount > 1 ? "Select only one option." : null;
    }

    private static string NumberBounds(decimal? min, decimal? max)
    {
        var low = min?.ToString(CultureInfo.InvariantCulture);
        var high = max?.ToString(CultureInfo.InvariantCulture);

        return (low, high) switch
        {
            (not null, not null) => $"Enter a number between {low} and {high}.",
            (not null, null) => $"Enter {low} or more.",
            (null, not null) => $"Enter {high} or less.",
            _ => "Enter a valid number.",
        };
    }

    private static string DateBounds(DateOnly? min, DateOnly? max)
    {
        var low = min?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var high = max?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return (low, high) switch
        {
            (not null, not null) => $"Pick a date between {low} and {high}.",
            (not null, null) => $"Pick a date on or after {low}.",
            (null, not null) => $"Pick a date on or before {high}.",
            _ => "Pick a valid date.",
        };
    }
}

using System.Globalization;
using PhieuFlow.Hub.Contracts.Forms;
using PhieuFlow.Hub.Contracts.Publishing;

namespace PhieuFlow.FormFiller.Validation;

/// <summary>
/// Client-side answer validation for the respondent app. A submission is published
/// fire-and-forget onto the RabbitMQ queue (ADR 0009), so the Hub cannot reject a bad
/// answer synchronously — the respondent app must refuse to publish until every answer
/// satisfies its question's constraints (<c>IsRequired</c>, Min/Max value, Min/Max length,
/// Min/Max selections).
/// </summary>
/// <remarks>
/// The messages mirror <c>QuestionInput.ConstraintHint</c>'s phrasing so the hint that
/// describes the rule and the error that reports the breach read the same way.
/// </remarks>
public sealed class SubmissionValidator
{
    private const string RequiredMessage = "An answer is required.";

    /// <summary>
    /// Checks every answer against its question. The value types are the ones
    /// <c>FillPage</c> stores in its answer map: <see cref="string"/> for text/number/date,
    /// <see cref="bool"/> for a single checkbox, <see cref="Guid"/> for a single choice and
    /// <c>IReadOnlySet&lt;Guid&gt;</c> for a checkbox group.
    /// </summary>
    /// <returns>
    /// One message per offending question, keyed by question id. An empty map means the
    /// form is ready to submit.
    /// </returns>
    public IReadOnlyDictionary<Guid, string> Validate(
        PublishedFormDto form,
        IReadOnlyDictionary<Guid, object?> answers)
    {
        var errors = new Dictionary<Guid, string>();

        foreach (var question in form.Pages.SelectMany(page => page.Questions))
        {
            var message = Check(question, answers.GetValueOrDefault(question.Id));
            if (message is not null)
            {
                errors[question.Id] = message;
            }
        }

        return errors;
    }

    private static string? Check(QuestionDto question, object? answer) => question switch
    {
        TextAreaQuestionDto q => CheckText(q, answer as string),
        NumberQuestionDto q => CheckNumber(q, answer as string),
        CalendarQuestionDto q => CheckDate(q, answer as string),
        CheckboxQuestionDto q => q.IsRequired && answer is not true ? RequiredMessage : null,
        CheckBoxGroupQuestionDto q => CheckGroup(q, answer as IReadOnlySet<Guid>),
        ChoiceQuestionDto q => q.IsRequired && answer is not Guid ? "Select an option." : null,
        _ => null,
    };

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

    private static string? CheckGroup(CheckBoxGroupQuestionDto question, IReadOnlySet<Guid>? selected)
    {
        var count = selected?.Count ?? 0;

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

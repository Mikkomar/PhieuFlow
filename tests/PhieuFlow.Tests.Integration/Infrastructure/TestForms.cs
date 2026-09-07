using PhieuFlow.Hub.Contracts.Forms;

namespace PhieuFlow.Tests.Integration.Infrastructure;

/// <summary>
/// Shared <see cref="FormDto"/> builders for the coverage-gap tests. The all-types form is
/// the one that drives <c>QuestionMapper</c>, <c>FormRequestMapper</c>/<c>FormResponseMapper</c>
/// and the <c>FormRepository</c> reconcile/clone switch arms through every question subtype.
/// </summary>
internal static class TestForms
{
    /// <summary>
    /// A draft form: one page carrying one question of every <see cref="QuestionDto"/> subtype,
    /// each with distinctive typed values so a round trip can assert field fidelity.
    /// </summary>
    public static FormDto AllQuestionTypes(Guid formId, string title = "All question types") => new()
    {
        Id = formId,
        Title = title,
        Description = "Every question type, once.",
        CreatedAt = DateTimeOffset.UtcNow,
        LastModifiedAt = DateTimeOffset.UtcNow,
        Revision = 1,
        VersionNumber = 1,
        Status = FormVersionStatusDto.Draft,
        Pages =
        [
            new FormPageDto
            {
                Id = Guid.NewGuid(),
                Title = "Page 1",
                Questions =
                [
                    new TextAreaQuestionDto
                    {
                        Id = Guid.NewGuid(), Text = "Free text", IsRequired = true,
                        MinLength = 2, MaxLength = 400,
                    },
                    new CheckboxQuestionDto
                    {
                        Id = Guid.NewGuid(), Text = "Agree to terms", IsRequired = true,
                        Label = "I agree",
                    },
                    new DropDownQuestionDto
                    {
                        Id = Guid.NewGuid(), Text = "Country", IsRequired = false,
                        Options = Options("Finland", "Vietnam", "Peru"),
                    },
                    new RadioButtonQuestionDto
                    {
                        Id = Guid.NewGuid(), Text = "Contract", IsRequired = true,
                        Options = Options("Permanent", "Fixed term"),
                    },
                    new CheckBoxGroupQuestionDto
                    {
                        Id = Guid.NewGuid(), Text = "Equipment", IsRequired = true,
                        MinSelections = 1, MaxSelections = 3,
                        Options = Options("Laptop", "Monitor", "Headset", "Phone"),
                    },
                    new NumberQuestionDto
                    {
                        Id = Guid.NewGuid(), Text = "Shoe size (EU)", IsRequired = false,
                        Min = 35.1234m, Max = 120.5m,
                    },
                    new CalendarQuestionDto
                    {
                        Id = Guid.NewGuid(), Text = "Start date", IsRequired = true,
                        MinDate = new DateOnly(2026, 1, 1), MaxDate = new DateOnly(2026, 12, 31),
                    },
                ],
            },
        ],
    };

    /// <summary>A minimal draft: one page, one text-area question.</summary>
    public static FormDto SingleTextQuestion(Guid formId, string title) => new()
    {
        Id = formId,
        Title = title,
        CreatedAt = DateTimeOffset.UtcNow,
        LastModifiedAt = DateTimeOffset.UtcNow,
        Revision = 1,
        VersionNumber = 1,
        Status = FormVersionStatusDto.Draft,
        Pages =
        [
            new FormPageDto
            {
                Id = Guid.NewGuid(),
                Title = "Page 1",
                Questions = [new TextAreaQuestionDto { Id = Guid.NewGuid(), Text = "Answer me", IsRequired = false }],
            },
        ],
    };

    public static List<QuestionOptionDto> Options(params string[] labels) =>
        [.. labels.Select((label, i) => new QuestionOptionDto { Id = Guid.NewGuid(), Label = label, Order = i })];
}

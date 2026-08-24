using PhieuFlow.Core.Entities;

namespace PhieuFlow.FormBuilder.Services;

/// <summary>
/// In-memory stand-in for the hub's form-management API (ADR 0001 describes that API; it
/// does not exist in this repo yet). Lets the builder round-trip between the forms list
/// and the edit route without a real backend.
/// </summary>
public class FormRepository
{
    private readonly Dictionary<Guid, Form> _forms;

    public FormRepository()
    {
        _forms = Seed().ToDictionary(f => f.Id);
    }

    public Form? TryGet(Guid id) => _forms.GetValueOrDefault(id);

    public Form CreateNew() => new()
    {
        Id = Guid.NewGuid(),
        Title = string.Empty,
        Pages = new List<FormPage> { new() { Id = Guid.NewGuid() } },
    };

    public void Save(Form form) => _forms[form.Id] = form;

    private static IEnumerable<Form> Seed()
    {
        yield return new Form
        {
            Id = Guid.Parse("a41c9f2e-0000-0000-0000-000000000001"),
            Title = "Employee onboarding checklist",
            Description = "Filled in by every new hire during their first week. Takes about ten minutes.",
            Pages = new List<FormPage>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Title = "Personal details",
                    Questions = new List<Question>
                    {
                        new TextAreaQuestion { Id = Guid.NewGuid(), Text = "Full name as it appears on your contract", IsRequired = true, MinLength = 2, MaxLength = 120 },
                        new DropDownQuestion
                        {
                            Id = Guid.NewGuid(), Text = "Which department are you joining?", IsRequired = true,
                            Options = new List<QuestionOption> { new() { Id = Guid.NewGuid(), Label = "Operations" }, new() { Id = Guid.NewGuid(), Label = "Finance" }, new() { Id = Guid.NewGuid(), Label = "Engineering" } },
                        },
                        new RadioButtonQuestion
                        {
                            Id = Guid.NewGuid(), Text = "Contract type", IsRequired = true,
                            Options = new List<QuestionOption> { new() { Id = Guid.NewGuid(), Label = "Permanent" }, new() { Id = Guid.NewGuid(), Label = "Fixed term" }, new() { Id = Guid.NewGuid(), Label = "Contractor" } },
                        },
                        new CheckboxQuestion { Id = Guid.NewGuid(), Text = "I have returned my signed contract", IsRequired = false },
                    },
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Title = "Equipment",
                    Questions = new List<Question>
                    {
                        new CheckBoxGroupQuestion
                        {
                            Id = Guid.NewGuid(), Text = "Which equipment do you need?", IsRequired = true, MinSelections = 1, MaxSelections = 3,
                            Options = new List<QuestionOption> { new() { Id = Guid.NewGuid(), Label = "Laptop" }, new() { Id = Guid.NewGuid(), Label = "Monitor" }, new() { Id = Guid.NewGuid(), Label = "Phone" }, new() { Id = Guid.NewGuid(), Label = "Headset" } },
                        },
                        new TextAreaQuestion { Id = Guid.NewGuid(), Text = "Accessibility requirements", IsRequired = false, MinLength = 0, MaxLength = 500 },
                    },
                },
                new() { Id = Guid.NewGuid(), Title = "Payroll", Questions = new List<Question>() },
            },
        };

        yield return new Form
        {
            Id = Guid.Parse("6d0b83a7-0000-0000-0000-000000000002"),
            Title = "Customer satisfaction survey Q3",
            Description = "Quarterly NPS and service feedback, sent to all active accounts at the end of the quarter.",
            Pages = new List<FormPage>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Title = "Feedback",
                    Questions = new List<Question>
                    {
                        new RadioButtonQuestion
                        {
                            Id = Guid.NewGuid(), Text = "How likely are you to recommend us to a colleague?", IsRequired = true,
                            Options = new List<QuestionOption> { new() { Id = Guid.NewGuid(), Label = "Very likely" }, new() { Id = Guid.NewGuid(), Label = "Neutral" }, new() { Id = Guid.NewGuid(), Label = "Unlikely" } },
                        },
                        new TextAreaQuestion { Id = Guid.NewGuid(), Text = "What could we do better?", IsRequired = false },
                    },
                },
            },
        };

        yield return new Form
        {
            Id = Guid.Parse("f7241be0-0000-0000-0000-000000000003"),
            Title = "Supplier compliance declaration",
            Description = "Procurement - annual renewal. Still missing the signature step and the certificate upload.",
            Pages = new List<FormPage>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Title = "Declaration",
                    Questions = new List<Question>
                    {
                        new CheckboxQuestion { Id = Guid.NewGuid(), Text = "We comply with all applicable labor and safety regulations", IsRequired = true },
                    },
                },
            },
        };

        yield return new Form
        {
            Id = Guid.Parse("2b95ca18-0000-0000-0000-000000000004"),
            Title = "Site safety inspection",
            Description = "Operations - weekly walkthrough. Any hazard answer triggers a follow-up photo field.",
            Pages = new List<FormPage> { new() { Id = Guid.NewGuid(), Title = "Walkthrough", Questions = new List<Question>() } },
        };

        yield return new Form
        {
            Id = Guid.Parse("0c58e6d4-0000-0000-0000-000000000005"),
            Title = "Training feedback - workshop series",
            Description = "Internal L&D. Short single-page form, waiting on the final session list before publishing.",
            Pages = new List<FormPage> { new() { Id = Guid.NewGuid(), Questions = new List<Question>() } },
        };

        yield return new Form
        {
            Id = Guid.Parse("93ae5107-0000-0000-0000-000000000006"),
            Title = "Incident report",
            Description = "Operations - unplanned events. Public link; submissions notify the duty manager.",
            Pages = new List<FormPage> { new() { Id = Guid.NewGuid(), Questions = new List<Question>() } },
        };

        yield return new Form
        {
            Id = Guid.Parse("5e12f8bb-0000-0000-0000-000000000007"),
            Title = "Vendor onboarding request",
            Description = "Finance - new supplier setup. Held back until the approval routing is confirmed.",
            Pages = new List<FormPage> { new() { Id = Guid.NewGuid(), Questions = new List<Question>() } },
        };
    }
}

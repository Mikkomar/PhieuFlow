using PhieuFlow.Core.Entities;

namespace PhieuFlow.SeedService.SampleData;

/// <summary>
/// Sample forms inserted into HubDatabase by the dev-only seeder.
/// </summary>
public static class SampleForms
{
    public static IEnumerable<FormVersion> All()
    {
        var now = DateTimeOffset.UtcNow;

        var onboardingFormId = Guid.Parse("a41c9f2e-0000-0000-0000-000000000001");
        var onboardingVersionId = Guid.NewGuid();
        var personalDetailsPageId = Guid.NewGuid();
        var equipmentPageId = Guid.NewGuid();
        var payrollPageId = Guid.NewGuid();
        yield return new FormVersion
        {
            Id = onboardingVersionId,
            FormId = onboardingFormId,
            Form = new Form { Id = onboardingFormId, CreatedAt = now },
            VersionNumber = 1,
            Status = FormVersionStatus.Published,
            PublishedAt = now,
            CreatedAt = now,
            LastModifiedAt = now,
            Title = "Employee onboarding checklist",
            Description = "Filled in by every new hire during their first week. Takes about ten minutes.",
            Pages = new List<FormPage>
            {
                new()
                {
                    Id = personalDetailsPageId,
                    FormVersionId = onboardingVersionId,
                    Title = "Personal details",
                    Order = 0,
                    Questions = new List<Question>
                    {
                        new TextAreaQuestion { Id = Guid.NewGuid(), FormPageId = personalDetailsPageId, Text = "Full name as it appears on your contract", IsRequired = true, Order = 0, MinLength = 2, MaxLength = 120 },
                        new DropDownQuestion
                        {
                            Id = Guid.NewGuid(), FormPageId = personalDetailsPageId, Text = "Which department are you joining?", IsRequired = true, Order = 1,
                            Options = new List<QuestionOption> { new() { Id = Guid.NewGuid(), Label = "Operations" }, new() { Id = Guid.NewGuid(), Label = "Finance" }, new() { Id = Guid.NewGuid(), Label = "Engineering" } },
                        },
                        new RadioButtonQuestion
                        {
                            Id = Guid.NewGuid(), FormPageId = personalDetailsPageId, Text = "Contract type", IsRequired = true, Order = 2,
                            Options = new List<QuestionOption> { new() { Id = Guid.NewGuid(), Label = "Permanent" }, new() { Id = Guid.NewGuid(), Label = "Fixed term" }, new() { Id = Guid.NewGuid(), Label = "Contractor" } },
                        },
                        new CheckboxQuestion { Id = Guid.NewGuid(), FormPageId = personalDetailsPageId, Text = "Signed contract", Label = "I have returned my signed contract", IsRequired = false, Order = 3 },
                    },
                },
                new()
                {
                    Id = equipmentPageId,
                    FormVersionId = onboardingVersionId,
                    Title = "Equipment",
                    Order = 1,
                    Questions = new List<Question>
                    {
                        new CheckBoxGroupQuestion
                        {
                            Id = Guid.NewGuid(), FormPageId = equipmentPageId, Text = "Which equipment do you need?", IsRequired = true, Order = 0, MinSelections = 1, MaxSelections = 3,
                            Options = new List<QuestionOption> { new() { Id = Guid.NewGuid(), Label = "Laptop" }, new() { Id = Guid.NewGuid(), Label = "Monitor" }, new() { Id = Guid.NewGuid(), Label = "Phone" }, new() { Id = Guid.NewGuid(), Label = "Headset" } },
                        },
                        new TextAreaQuestion { Id = Guid.NewGuid(), FormPageId = equipmentPageId, Text = "Accessibility requirements", IsRequired = false, Order = 1, MinLength = 0, MaxLength = 500 },
                        new NumberQuestion { Id = Guid.NewGuid(), FormPageId = equipmentPageId, Text = "Shoe size (EU)", IsRequired = false, Order = 2, Min = 35, Max = 50 },
                        new CalendarQuestion { Id = Guid.NewGuid(), FormPageId = equipmentPageId, Text = "Preferred start date", IsRequired = true, Order = 3, MinDate = new DateOnly(2026, 1, 1), MaxDate = new DateOnly(2026, 12, 31) },
                    },
                },
                new() { Id = payrollPageId, FormVersionId = onboardingVersionId, Title = "Payroll", Order = 2, Questions = new List<Question>() },
            },
        };

        var satisfactionFormId = Guid.Parse("6d0b83a7-0000-0000-0000-000000000002");
        var satisfactionVersionId = Guid.NewGuid();
        var feedbackPageId = Guid.NewGuid();
        yield return new FormVersion
        {
            Id = satisfactionVersionId,
            FormId = satisfactionFormId,
            Form = new Form { Id = satisfactionFormId, CreatedAt = now },
            VersionNumber = 1,
            Status = FormVersionStatus.Published,
            PublishedAt = now,
            CreatedAt = now,
            LastModifiedAt = now,
            Title = "Customer satisfaction survey Q3",
            Description = "Quarterly NPS and service feedback, sent to all active accounts at the end of the quarter.",
            Pages = new List<FormPage>
            {
                new()
                {
                    Id = feedbackPageId,
                    FormVersionId = satisfactionVersionId,
                    Title = "Feedback",
                    Questions = new List<Question>
                    {
                        new RadioButtonQuestion
                        {
                            Id = Guid.NewGuid(), FormPageId = feedbackPageId, Text = "How likely are you to recommend us to a colleague?", IsRequired = true, Order = 0,
                            Options = new List<QuestionOption> { new() { Id = Guid.NewGuid(), Label = "Very likely" }, new() { Id = Guid.NewGuid(), Label = "Neutral" }, new() { Id = Guid.NewGuid(), Label = "Unlikely" } },
                        },
                        new TextAreaQuestion { Id = Guid.NewGuid(), FormPageId = feedbackPageId, Text = "What could we do better?", IsRequired = false, Order = 1 },
                    },
                },
            },
        };

        var complianceFormId = Guid.Parse("f7241be0-0000-0000-0000-000000000003");
        var complianceVersionId = Guid.NewGuid();
        var declarationPageId = Guid.NewGuid();
        yield return new FormVersion
        {
            Id = complianceVersionId,
            FormId = complianceFormId,
            Form = new Form { Id = complianceFormId, CreatedAt = now },
            VersionNumber = 1,
            Status = FormVersionStatus.Draft,
            CreatedAt = now,
            LastModifiedAt = now,
            Title = "Supplier compliance declaration",
            Description = "Procurement - annual renewal. Still missing the signature step and the certificate upload.",
            Pages = new List<FormPage>
            {
                new()
                {
                    Id = declarationPageId,
                    FormVersionId = complianceVersionId,
                    Title = "Declaration",
                    Questions = new List<Question>
                    {
                        new CheckboxQuestion { Id = Guid.NewGuid(), FormPageId = declarationPageId, Text = "Regulatory compliance", Label = "We comply with all applicable labor and safety regulations", IsRequired = true },
                    },
                },
            },
        };

        var safetyFormId = Guid.Parse("2b95ca18-0000-0000-0000-000000000004");
        var safetyVersionId = Guid.NewGuid();
        var walkthroughPageId = Guid.NewGuid();
        yield return new FormVersion
        {
            Id = safetyVersionId,
            FormId = safetyFormId,
            Form = new Form { Id = safetyFormId, CreatedAt = now },
            VersionNumber = 1,
            Status = FormVersionStatus.Published,
            PublishedAt = now,
            CreatedAt = now,
            LastModifiedAt = now,
            Title = "Site safety inspection",
            Description = "Operations - weekly walkthrough. Any hazard answer triggers a follow-up photo field.",
            Pages = new List<FormPage> { new() { Id = walkthroughPageId, FormVersionId = safetyVersionId, Title = "Walkthrough", Questions = new List<Question>() } },
        };

        var trainingFormId = Guid.Parse("0c58e6d4-0000-0000-0000-000000000005");
        var trainingVersionId = Guid.NewGuid();
        var trainingPageId = Guid.NewGuid();
        yield return new FormVersion
        {
            Id = trainingVersionId,
            FormId = trainingFormId,
            Form = new Form { Id = trainingFormId, CreatedAt = now },
            VersionNumber = 1,
            Status = FormVersionStatus.Draft,
            CreatedAt = now,
            LastModifiedAt = now,
            Title = "Training feedback - workshop series",
            Description = "Internal L&D. Short single-page form, waiting on the final session list before publishing.",
            Pages = new List<FormPage> { new() { Id = trainingPageId, FormVersionId = trainingVersionId, Questions = new List<Question>() } },
        };

        var incidentFormId = Guid.Parse("93ae5107-0000-0000-0000-000000000006");
        var incidentVersionId = Guid.NewGuid();
        var incidentPageId = Guid.NewGuid();
        yield return new FormVersion
        {
            Id = incidentVersionId,
            FormId = incidentFormId,
            Form = new Form { Id = incidentFormId, CreatedAt = now },
            VersionNumber = 1,
            Status = FormVersionStatus.Published,
            PublishedAt = now,
            CreatedAt = now,
            LastModifiedAt = now,
            Title = "Incident report",
            Description = "Operations - unplanned events. Public link; submissions notify the duty manager.",
            Pages = new List<FormPage> { new() { Id = incidentPageId, FormVersionId = incidentVersionId, Questions = new List<Question>() } },
        };

        var vendorFormId = Guid.Parse("5e12f8bb-0000-0000-0000-000000000007");
        var vendorVersionId = Guid.NewGuid();
        var vendorPageId = Guid.NewGuid();
        yield return new FormVersion
        {
            Id = vendorVersionId,
            FormId = vendorFormId,
            Form = new Form { Id = vendorFormId, CreatedAt = now },
            VersionNumber = 1,
            Status = FormVersionStatus.Draft,
            CreatedAt = now,
            LastModifiedAt = now,
            Title = "Vendor onboarding request",
            Description = "Finance - new supplier setup. Held back until the approval routing is confirmed.",
            Pages = new List<FormPage> { new() { Id = vendorPageId, FormVersionId = vendorVersionId, Questions = new List<Question>() } },
        };
    }
}

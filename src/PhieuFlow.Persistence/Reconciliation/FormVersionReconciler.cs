using PhieuFlow.Core.Entities;

namespace PhieuFlow.Persistence.Reconciliation;

/// <inheritdoc />
public sealed class FormVersionReconciler(IFormTreeCloner treeCloner) : IFormVersionReconciler
{
    public FormVersionReconcileResult Reconcile(
        FormVersion currentVersion, FormVersion incomingContent, DateTimeOffset timestamp)
    {
        if (currentVersion.Status == FormVersionStatus.Draft)
        {
            currentVersion.Title = incomingContent.Title;
            currentVersion.Description = incomingContent.Description;
            currentVersion.LastModifiedAt = timestamp;
            currentVersion.LastModifiedBy = incomingContent.LastModifiedBy;
            currentVersion.Revision += 1;

            ReconcilePages(currentVersion, incomingContent.Pages);
            return FormVersionReconcileResult.UpdatedInPlace(currentVersion);
        }

        // Published: currentVersion is immutable from here on. Fork a new draft.
        var forkedVersionId = Guid.NewGuid();

        var forked = new FormVersion
        {
            Id = forkedVersionId,
            FormId = currentVersion.FormId,
            VersionNumber = currentVersion.VersionNumber + 1,
            Status = FormVersionStatus.Draft,
            Title = incomingContent.Title,
            Description = incomingContent.Description,
            Revision = 1,
            CreatedAt = timestamp,
            LastModifiedAt = timestamp,
            LastModifiedBy = incomingContent.LastModifiedBy,
            Pages = incomingContent.Pages.Select(p => treeCloner.ClonePageWithFreshIds(p, forkedVersionId)).ToList(),
        };

        return FormVersionReconcileResult.Forked(forked);
    }

    private static void ReconcilePages(FormVersion existingVersion, ICollection<FormPage> incomingPages)
    {
        var existingById = existingVersion.Pages.ToDictionary(p => p.Id);
        var incomingIds = incomingPages.Select(p => p.Id).ToHashSet();

        foreach (var stale in existingVersion.Pages.Where(p => !incomingIds.Contains(p.Id)).ToList())
        {
            existingVersion.Pages.Remove(stale);
        }

        foreach (var incomingPage in incomingPages)
        {
            if (existingById.TryGetValue(incomingPage.Id, out var trackedPage))
            {
                trackedPage.Title = incomingPage.Title;
                trackedPage.Order = incomingPage.Order;
                ReconcileQuestions(trackedPage, incomingPage.Questions);
            }
            else
            {
                incomingPage.FormVersionId = existingVersion.Id;
                existingVersion.Pages.Add(incomingPage);
            }
        }
    }

    private static void ReconcileQuestions(FormPage existingPage, ICollection<Question> incomingQuestions)
    {
        var existingById = existingPage.Questions.ToDictionary(q => q.Id);
        var incomingIds = incomingQuestions.Select(q => q.Id).ToHashSet();

        foreach (var stale in existingPage.Questions.Where(q => !incomingIds.Contains(q.Id)).ToList())
        {
            existingPage.Questions.Remove(stale);
        }

        foreach (var incoming in incomingQuestions)
        {
            if (existingById.TryGetValue(incoming.Id, out var tracked))
            {
                if (tracked.GetType() != incoming.GetType())
                {
                    throw new InvalidOperationException(
                        $"Question {incoming.Id} changed type from '{tracked.GetType().Name}' to '{incoming.GetType().Name}'. " +
                        "Questions never change type after creation, so this indicates a client bug or a hand-crafted request.");
                }

                UpdateQuestionFields(tracked, incoming);
            }
            else
            {
                incoming.FormPageId = existingPage.Id;
                existingPage.Questions.Add(incoming);
            }
        }
    }

    private static void UpdateQuestionFields(Question tracked, Question incoming)
    {
        tracked.Text = incoming.Text;
        tracked.IsRequired = incoming.IsRequired;
        tracked.Order = incoming.Order;

        switch (tracked)
        {
            case TextAreaQuestion t when incoming is TextAreaQuestion i:
                t.MinLength = i.MinLength;
                t.MaxLength = i.MaxLength;
                break;
            case CheckboxQuestion t when incoming is CheckboxQuestion i:
                t.Label = i.Label;
                break;
            case NumberQuestion t when incoming is NumberQuestion i:
                t.Min = i.Min;
                t.Max = i.Max;
                break;
            case CalendarQuestion t when incoming is CalendarQuestion i:
                t.MinDate = i.MinDate;
                t.MaxDate = i.MaxDate;
                break;
            case CheckBoxGroupQuestion t when incoming is CheckBoxGroupQuestion i:
                t.MinSelections = i.MinSelections;
                t.MaxSelections = i.MaxSelections;
                ReconcileOptions(t, i.Options);
                break;
            case ChoiceQuestion t when incoming is ChoiceQuestion i:
                ReconcileOptions(t, i.Options);
                break;
        }
    }

    private static void ReconcileOptions(ChoiceQuestion tracked, ICollection<QuestionOption> incomingOptions)
    {
        var existingById = tracked.Options.ToDictionary(o => o.Id);
        var incomingIds = incomingOptions.Select(o => o.Id).ToHashSet();

        foreach (var stale in tracked.Options.Where(o => !incomingIds.Contains(o.Id)).ToList())
        {
            tracked.Options.Remove(stale);
        }

        foreach (var incoming in incomingOptions)
        {
            if (existingById.TryGetValue(incoming.Id, out var trackedOption))
            {
                trackedOption.Label = incoming.Label;
                trackedOption.Order = incoming.Order;
            }
            else
            {
                tracked.Options.Add(incoming);
            }
        }
    }
}

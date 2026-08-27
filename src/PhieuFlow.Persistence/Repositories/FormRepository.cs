using Microsoft.EntityFrameworkCore;
using PhieuFlow.Core.Entities;
using PhieuFlow.Persistence.Projections;

namespace PhieuFlow.Persistence.Repositories;

public class FormRepository(HubDbContext dbContext) : IFormRepository
{
    public async Task<Form?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var form = await dbContext.Forms
            .AsNoTracking()
            .AsSplitQuery()
            .Include(f => f.Pages.OrderBy(p => p.Order))
                .ThenInclude(p => p.Questions)
                    .ThenInclude(q => (q as ChoiceQuestion)!.Options)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

        if (form is not null)
        {
            foreach (var page in form.Pages)
            {
                page.Questions = page.Questions.OrderBy(q => q.Order).ToList();
            }
        }

        return form;
    }

    public async Task SaveAsync(Form form, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.Forms
            .AsSplitQuery()
            .Include(f => f.Pages)
                .ThenInclude(p => p.Questions)
                    .ThenInclude(q => (q as ChoiceQuestion)!.Options)
            .FirstOrDefaultAsync(f => f.Id == form.Id, cancellationToken);

        var now = DateTimeOffset.UtcNow;

        if (existing is null)
        {
            form.CreatedAt = now;
            form.LastModifiedAt = now;
            dbContext.Forms.Add(form);
            return;
        }

        existing.Title = form.Title;
        existing.Description = form.Description;
        existing.LastModifiedAt = now;
        existing.LastModifiedBy = form.LastModifiedBy;

        ReconcilePages(existing, form.Pages);
    }

    private static void ReconcilePages(Form existingForm, ICollection<FormPage> incomingPages)
    {
        var existingById = existingForm.Pages.ToDictionary(p => p.Id);
        var incomingIds = incomingPages.Select(p => p.Id).ToHashSet();

        foreach (var stale in existingForm.Pages.Where(p => !incomingIds.Contains(p.Id)).ToList())
        {
            existingForm.Pages.Remove(stale);
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
                incomingPage.FormId = existingForm.Id;
                existingForm.Pages.Add(incomingPage);
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
            }
            else
            {
                tracked.Options.Add(incoming);
            }
        }
    }

    public async Task<FormBatchResult> GetBatchAsync(Guid? startId, int take, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Forms.AsNoTracking().AsQueryable();
        if (startId is not null)
        {
            query = query.Where(f => f.Id >= startId.Value);
        }

        var page = await query
            .OrderBy(f => f.Id)
            .Take(take + 1)
            .Select(f => new FormListItem
            {
                Id = f.Id,
                Title = f.Title,
                Description = f.Description,
                CreatedAt = f.CreatedAt,
                LastModifiedAt = f.LastModifiedAt,
                LastModifiedBy = f.LastModifiedBy,
                Revision = f.Revision,
                PageCount = f.Pages.Count,
                QuestionCount = f.Pages.Sum(p => p.Questions.Count),
            })
            .ToListAsync(cancellationToken);

        Guid? nextStartId = null;
        if (page.Count > take)
        {
            nextStartId = page[take].Id;
            page.RemoveAt(take);
        }

        return new FormBatchResult
        {
            Items = page,
            NextStartId = nextStartId,
        };
    }
}

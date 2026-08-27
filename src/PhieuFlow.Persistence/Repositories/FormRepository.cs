using Microsoft.EntityFrameworkCore;
using PhieuFlow.Core.Entities;
using PhieuFlow.Persistence.Projections;

namespace PhieuFlow.Persistence.Repositories;

public class FormRepository(HubDbContext dbContext) : IFormRepository
{
    public async Task<FormVersion?> GetByIdAsync(Guid formId, CancellationToken cancellationToken = default)
    {
        var version = await dbContext.FormVersions
            .AsNoTracking()
            .AsSplitQuery()
            .Where(v => v.FormId == formId)
            .OrderByDescending(v => v.VersionNumber)
            .Include(v => v.Pages.OrderBy(p => p.Order))
                .ThenInclude(p => p.Questions)
                    .ThenInclude(q => (q as ChoiceQuestion)!.Options)
            .FirstOrDefaultAsync(cancellationToken);

        if (version is not null)
        {
            foreach (var page in version.Pages)
            {
                page.Questions = page.Questions.OrderBy(q => q.Order).ToList();
            }
        }

        return version;
    }

    public async Task SaveAsync(Guid formId, FormVersion incomingContent, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        var currentVersion = await dbContext.FormVersions
            .AsSplitQuery()
            .Where(v => v.FormId == formId)
            .OrderByDescending(v => v.VersionNumber)
            .Include(v => v.Pages)
                .ThenInclude(p => p.Questions)
                    .ThenInclude(q => (q as ChoiceQuestion)!.Options)
            .FirstOrDefaultAsync(cancellationToken);

        if (currentVersion is null)
        {
            var newVersionId = Guid.NewGuid();

            foreach (var page in incomingContent.Pages)
            {
                page.FormVersionId = newVersionId;
            }

            dbContext.Forms.Add(new Form { Id = formId, CreatedAt = now });

            dbContext.FormVersions.Add(new FormVersion
            {
                Id = newVersionId,
                FormId = formId,
                VersionNumber = 1,
                Status = FormVersionStatus.Draft,
                Title = incomingContent.Title,
                Description = incomingContent.Description,
                Revision = 1,
                CreatedAt = now,
                LastModifiedAt = now,
                LastModifiedBy = incomingContent.LastModifiedBy,
                Pages = incomingContent.Pages,
            });

            return;
        }

        if (currentVersion.Status == FormVersionStatus.Draft)
        {
            currentVersion.Title = incomingContent.Title;
            currentVersion.Description = incomingContent.Description;
            currentVersion.LastModifiedAt = now;
            currentVersion.LastModifiedBy = incomingContent.LastModifiedBy;
            currentVersion.Revision += 1;

            ReconcilePages(currentVersion, incomingContent.Pages);
            return;
        }

        // Published: currentVersion is immutable from here on. Fork a new draft.
        var forkedVersionId = Guid.NewGuid();

        dbContext.FormVersions.Add(new FormVersion
        {
            Id = forkedVersionId,
            FormId = formId,
            VersionNumber = currentVersion.VersionNumber + 1,
            Status = FormVersionStatus.Draft,
            Title = incomingContent.Title,
            Description = incomingContent.Description,
            Revision = 1,
            CreatedAt = now,
            LastModifiedAt = now,
            LastModifiedBy = incomingContent.LastModifiedBy,
            Pages = incomingContent.Pages.Select(p => ClonePageWithFreshIds(p, forkedVersionId)).ToList(),
        });
    }

    public async Task<bool> PublishAsync(Guid formId, CancellationToken cancellationToken = default)
    {
        var currentVersion = await dbContext.FormVersions
            .Where(v => v.FormId == formId)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (currentVersion is null)
        {
            return false;
        }

        if (currentVersion.Status == FormVersionStatus.Published)
        {
            return true;
        }

        currentVersion.Status = FormVersionStatus.Published;
        currentVersion.PublishedAt = DateTimeOffset.UtcNow;
        return true;
    }

    private static FormPage ClonePageWithFreshIds(FormPage source, Guid newVersionId)
    {
        var newPageId = Guid.NewGuid();
        return new FormPage
        {
            Id = newPageId,
            FormVersionId = newVersionId,
            Title = source.Title,
            Order = source.Order,
            Questions = source.Questions.Select(q => CloneQuestionWithFreshIds(q, newPageId)).ToList(),
        };
    }

    private static Question CloneQuestionWithFreshIds(Question source, Guid newPageId)
    {
        var newQuestionId = Guid.NewGuid();
        return source switch
        {
            TextAreaQuestion q => new TextAreaQuestion
            {
                Id = newQuestionId, FormPageId = newPageId, Text = q.Text, IsRequired = q.IsRequired, Order = q.Order,
                MinLength = q.MinLength, MaxLength = q.MaxLength,
            },
            CheckboxQuestion q => new CheckboxQuestion
            {
                Id = newQuestionId, FormPageId = newPageId, Text = q.Text, IsRequired = q.IsRequired, Order = q.Order,
                Label = q.Label,
            },
            DropDownQuestion q => new DropDownQuestion
            {
                Id = newQuestionId, FormPageId = newPageId, Text = q.Text, IsRequired = q.IsRequired, Order = q.Order,
                Options = CloneOptionsWithFreshIds(q.Options),
            },
            RadioButtonQuestion q => new RadioButtonQuestion
            {
                Id = newQuestionId, FormPageId = newPageId, Text = q.Text, IsRequired = q.IsRequired, Order = q.Order,
                Options = CloneOptionsWithFreshIds(q.Options),
            },
            CheckBoxGroupQuestion q => new CheckBoxGroupQuestion
            {
                Id = newQuestionId, FormPageId = newPageId, Text = q.Text, IsRequired = q.IsRequired, Order = q.Order,
                Options = CloneOptionsWithFreshIds(q.Options),
                MinSelections = q.MinSelections, MaxSelections = q.MaxSelections,
            },
            NumberQuestion q => new NumberQuestion
            {
                Id = newQuestionId, FormPageId = newPageId, Text = q.Text, IsRequired = q.IsRequired, Order = q.Order,
                Min = q.Min, Max = q.Max,
            },
            CalendarQuestion q => new CalendarQuestion
            {
                Id = newQuestionId, FormPageId = newPageId, Text = q.Text, IsRequired = q.IsRequired, Order = q.Order,
                MinDate = q.MinDate, MaxDate = q.MaxDate,
            },
            _ => throw new NotSupportedException($"Unknown question type '{source.GetType().Name}'."),
        };
    }

    private static List<QuestionOption> CloneOptionsWithFreshIds(IEnumerable<QuestionOption> options) => options
        .Select(o => new QuestionOption { Id = Guid.NewGuid(), Label = o.Label })
        .ToList();

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
            }
            else
            {
                tracked.Options.Add(incoming);
            }
        }
    }

    public async Task<FormBatchResult> GetBatchAsync(Guid? startId, int take, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Forms
            .AsNoTracking()
            .Select(f => new
            {
                f.Id,
                f.CreatedAt,
                CurrentVersion = f.Versions.OrderByDescending(v => v.VersionNumber).First(),
            });

        if (startId is not null)
        {
            query = query.Where(x => x.Id >= startId.Value);
        }

        var page = await query
            .OrderBy(x => x.Id)
            .Take(take + 1)
            .Select(x => new FormListItem
            {
                Id = x.Id,
                Title = x.CurrentVersion.Title,
                Description = x.CurrentVersion.Description,
                CreatedAt = x.CreatedAt,
                LastModifiedAt = x.CurrentVersion.LastModifiedAt,
                LastModifiedBy = x.CurrentVersion.LastModifiedBy,
                Revision = x.CurrentVersion.Revision,
                VersionNumber = x.CurrentVersion.VersionNumber,
                Status = x.CurrentVersion.Status,
                PageCount = x.CurrentVersion.Pages.Count,
                QuestionCount = x.CurrentVersion.Pages.Sum(p => p.Questions.Count),
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

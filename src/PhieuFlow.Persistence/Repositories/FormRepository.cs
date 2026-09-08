using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PhieuFlow.Core.Entities;
using PhieuFlow.Persistence.Projections;
using PhieuFlow.Persistence.Reconciliation;

namespace PhieuFlow.Persistence.Repositories;

public class FormRepository(
    HubDbContext dbContext,
    IFormVersionReconciler reconciler,
    IFormTreeCloner treeCloner,
    ILogger<FormRepository> logger) : IFormRepository
{
    public Task<Guid> CreateAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var formId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        dbContext.Forms.Add(new Form { Id = formId, CreatedAt = now });
        dbContext.FormVersions.Add(new FormVersion
        {
            Id = versionId,
            FormId = formId,
            VersionNumber = 1,
            Status = FormVersionStatus.Draft,
            Title = string.Empty,
            Revision = 1,
            CreatedAt = now,
            LastModifiedAt = now,
            Pages = new List<FormPage>
            {
                new() { Id = Guid.NewGuid(), FormVersionId = versionId, Order = 0 },
            },
        });

        return Task.FromResult(formId);
    }

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

                foreach (var question in page.Questions.OfType<ChoiceQuestion>())
                {
                    question.Options = question.Options.OrderBy(o => o.Order).ToList();
                }
            }
        }

        return version;
    }

    public async Task<FormVersion?> GetPublishedByIdAsync(Guid formId, CancellationToken cancellationToken = default)
    {
        var version = await dbContext.FormVersions
            .AsNoTracking()
            .AsSplitQuery()
            .Where(v => v.FormId == formId && v.Status == FormVersionStatus.Published)
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

                foreach (var question in page.Questions.OfType<ChoiceQuestion>())
                {
                    question.Options = question.Options.OrderBy(o => o.Order).ToList();
                }
            }
        }

        return version;
    }

    public async Task<FormVersion?> GetPublishedVersionAsync(Guid formId, int versionNumber, CancellationToken cancellationToken = default)
    {
        var version = await dbContext.FormVersions
            .AsNoTracking()
            .AsSplitQuery()
            .Where(v => v.FormId == formId
                && v.VersionNumber == versionNumber
                && v.Status == FormVersionStatus.Published)
            .Include(v => v.Pages.OrderBy(p => p.Order))
                .ThenInclude(p => p.Questions)
                    .ThenInclude(q => (q as ChoiceQuestion)!.Options)
            .FirstOrDefaultAsync(cancellationToken);

        if (version is not null)
        {
            foreach (var page in version.Pages)
            {
                page.Questions = page.Questions.OrderBy(q => q.Order).ToList();

                foreach (var question in page.Questions.OfType<ChoiceQuestion>())
                {
                    question.Options = question.Options.OrderBy(o => o.Order).ToList();
                }
            }
        }

        return version;
    }

    public async Task<FormSaveResult> SaveAsync(Guid formId, FormVersion incomingContent, CancellationToken cancellationToken = default)
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
            // No form for this id (never existed, or deleted while a builder tab stayed open):
            // never recreate it from a client-supplied id. Creation is POST /forms only.
            return FormSaveResult.NotFound;
        }

        // Optimistic concurrency (ADR 0002/0007): the save must target the exact row the server
        // holds now. VersionNumber is part of the key because a fork resets Revision to 1, so a
        // stale client can otherwise collide with a different version's Revision and clobber it
        // via ReconcilePages. The server never trusts these incoming numbers for the write.
        if (incomingContent.VersionNumber != currentVersion.VersionNumber
            || incomingContent.Revision != currentVersion.Revision)
        {
            return FormSaveResult.Conflict;
        }

        try
        {
            // Versioning policy (fork-on-publish-edit, tree reconciliation) lives in the
            // reconciler (ADR 0007); this method only loads, guards concurrency, and persists.
            var outcome = reconciler.Reconcile(currentVersion, incomingContent, now);
            if (outcome.IsFork)
            {
                dbContext.FormVersions.Add(outcome.Version);
            }

            return FormSaveResult.Saved(ToVersionState(outcome.Version));
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
        {
            // A question changed type, or carried an unknown type — a client bug or a
            // hand-crafted request. Reaches the endpoint as a bare 500 without this.
            logger.LogError(ex, "Reconciling the save for form {FormId} failed.", formId);
            throw;
        }
    }

    private static FormVersionState ToVersionState(FormVersion version) => new()
    {
        VersionNumber = version.VersionNumber,
        Revision = version.Revision,
        Status = version.Status,
        LastModifiedAt = version.LastModifiedAt,
        PublishedAt = version.PublishedAt,
    };

    public async Task<int?> GetLatestPublishedVersionNumberAsync(Guid formId, CancellationToken cancellationToken = default)
    {
        return await dbContext.FormVersions
            .AsNoTracking()
            .Where(v => v.FormId == formId && v.Status == FormVersionStatus.Published)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => (int?)v.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<FormPublishResult> PublishAsync(Guid formId, int expectedVersionNumber, int expectedRevision, CancellationToken cancellationToken = default)
    {
        var currentVersion = await dbContext.FormVersions
            .Where(v => v.FormId == formId)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (currentVersion is null)
        {
            return FormPublishResult.NotFound;
        }

        if (currentVersion.VersionNumber != expectedVersionNumber || currentVersion.Revision != expectedRevision)
        {
            return FormPublishResult.Conflict;
        }

        if (currentVersion.Status != FormVersionStatus.Published)
        {
            currentVersion.Status = FormVersionStatus.Published;
            currentVersion.PublishedAt = DateTimeOffset.UtcNow;
        }

        return FormPublishResult.Published(ToVersionState(currentVersion));
    }

    public async Task<FormDeleteResult> DeleteAsync(Guid formId, CancellationToken cancellationToken = default)
    {
        var form = await dbContext.Forms.FirstOrDefaultAsync(f => f.Id == formId, cancellationToken);
        if (form is null)
        {
            return FormDeleteResult.NotFound;
        }

        // FormSubmission -> Form / FormVersion FKs are DeleteBehavior.Restrict
        // (FormSubmissionConfiguration): a submission is a historical record that must outlive
        // the form. Refuse the delete cleanly here rather than letting SaveChanges hit the FK
        // and surface as a bare 500. Backed by IX_FormSubmissions_FormId.
        if (await dbContext.FormSubmissions.AnyAsync(s => s.FormId == formId, cancellationToken))
        {
            return FormDeleteResult.Blocked;
        }

        // Versions -> pages -> questions -> options all cascade (see FormConfiguration /
        // FormVersionConfiguration).
        dbContext.Forms.Remove(form);
        return FormDeleteResult.Deleted;
    }

    public async Task<Guid?> DuplicateAsync(Guid sourceId, CancellationToken cancellationToken = default)
    {
        var source = await dbContext.FormVersions
            .AsNoTracking()
            .AsSplitQuery()
            .Where(v => v.FormId == sourceId)
            .OrderByDescending(v => v.VersionNumber)
            .Include(v => v.Pages.OrderBy(p => p.Order))
                .ThenInclude(p => p.Questions)
                    .ThenInclude(q => (q as ChoiceQuestion)!.Options)
            .FirstOrDefaultAsync(cancellationToken);

        if (source is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var newFormId = Guid.NewGuid();
        var newVersionId = Guid.NewGuid();

        dbContext.Forms.Add(new Form { Id = newFormId, CreatedAt = now });

        List<FormPage> clonedPages;
        try
        {
            clonedPages = source.Pages.Select(p => treeCloner.ClonePageWithFreshIds(p, newVersionId)).ToList();
        }
        catch (NotSupportedException ex)
        {
            logger.LogError(ex, "Duplicating form {SourceId} failed on an unknown question type.", sourceId);
            throw;
        }

        dbContext.FormVersions.Add(new FormVersion
        {
            Id = newVersionId,
            FormId = newFormId,
            VersionNumber = 1,
            Status = FormVersionStatus.Draft,
            Title = string.IsNullOrWhiteSpace(source.Title) ? "Copy of untitled form" : $"Copy of {source.Title}",
            Description = source.Description,
            Revision = 1,
            CreatedAt = now,
            LastModifiedAt = now,
            Pages = clonedPages,
        });

        return newFormId;
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
                LatestPublished = f.Versions
                    .Where(v => v.Status == FormVersionStatus.Published)
                    .OrderByDescending(v => v.VersionNumber)
                    .Select(v => new { v.VersionNumber, v.PublishedAt })
                    .FirstOrDefault(),
                HasSubmissions = dbContext.FormSubmissions.Any(s => s.FormId == f.Id),
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
                LatestPublishedVersionNumber = x.LatestPublished != null ? x.LatestPublished.VersionNumber : null,
                LatestPublishedAt = x.LatestPublished != null ? x.LatestPublished.PublishedAt : null,
                PageCount = x.CurrentVersion.Pages.Count,
                QuestionCount = x.CurrentVersion.Pages.Sum(p => p.Questions.Count),
                HasSubmissions = x.HasSubmissions,
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

    public async Task<SubmissionBatchResult?> GetSubmissionsBatchAsync(
        Guid formId, Guid? startId, int take, CancellationToken cancellationToken = default)
    {
        // Only the first page pays for the existence check; later pages are a continuation of
        // a form we've already confirmed. A form with no submissions still returns an empty
        // batch, not 404.
        if (startId is null && !await dbContext.Forms.AnyAsync(f => f.Id == formId, cancellationToken))
        {
            return null;
        }

        var query = dbContext.FormSubmissions
            .AsNoTracking()
            .Where(s => s.FormId == formId);

        if (startId is not null)
        {
            query = query.Where(s => s.Id >= startId.Value);
        }

        var page = await query
            .OrderBy(s => s.Id)
            .Take(take + 1)
            .Include(s => s.Answers)
            .ToListAsync(cancellationToken);

        Guid? nextStartId = null;
        if (page.Count > take)
        {
            nextStartId = page[take].Id;
            page.RemoveAt(take);
        }

        // Resolve option ids to labels from the exact versions these submissions reference.
        // Published versions are immutable (ADR 0007), so the id always resolves; the same
        // Include shape as GetByIdAsync keeps the query translatable.
        var versionIds = page.Select(s => s.FormVersionId).Distinct().ToList();
        var optionLabels = (await dbContext.FormVersions
                .AsNoTracking()
                .AsSplitQuery()
                .Where(v => versionIds.Contains(v.Id))
                .Include(v => v.Pages)
                    .ThenInclude(p => p.Questions)
                        .ThenInclude(q => (q as ChoiceQuestion)!.Options)
                .ToListAsync(cancellationToken))
            .SelectMany(v => v.Pages)
            .SelectMany(p => p.Questions)
            .OfType<ChoiceQuestion>()
            .SelectMany(q => q.Options)
            .GroupBy(o => o.Id)
            .ToDictionary(g => g.Key, g => g.First().Label);

        var items = page
            .Select(s => new SubmissionListItem
            {
                Id = s.Id,
                SubmittedAt = s.SubmittedAt,
                FormVersionNumber = s.FormVersionNumber,
                Answers = s.Answers
                    .GroupBy(a => a.QuestionId)
                    .Select(g => new SubmissionAnswerItem
                    {
                        QuestionId = g.Key,
                        QuestionText = g.First().QuestionText,
                        Order = g.Min(a => a.Order),
                        Value = DisplayValue(g, optionLabels),
                    })
                    .OrderBy(a => a.Order)
                    .ToList(),
            })
            .ToList();

        return new SubmissionBatchResult { Items = items, NextStartId = nextStartId };
    }

    // One question's answer rows -> a single display string. Choice questions may bring more
    // than one row (a checkbox group); value/boolean questions bring exactly one.
    private static string? DisplayValue(
        IEnumerable<SubmissionAnswer> answers, IReadOnlyDictionary<Guid, string> optionLabels)
    {
        var rows = answers.OrderBy(a => a.Order).ToList();

        var options = rows.OfType<OptionSubmissionAnswer>().ToList();
        if (options.Count > 0)
        {
            return string.Join(", ", options.Select(o =>
                optionLabels.TryGetValue(o.OptionId, out var label) ? label : "(unknown option)"));
        }

        return rows[0] switch
        {
            BooleanSubmissionAnswer b => b.Checked ? "Yes" : "No",
            ValueSubmissionAnswer v => v.Value,
            _ => null,
        };
    }

    public async Task<PublishedFormBatchResult> GetPublishedBatchAsync(Guid? startId, int take, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Forms
            .AsNoTracking()
            .Select(f => new
            {
                f.Id,
                LatestPublished = f.Versions
                    .Where(v => v.Status == FormVersionStatus.Published)
                    .OrderByDescending(v => v.VersionNumber)
                    .Select(v => new { v.Title, v.Description, v.VersionNumber, v.PublishedAt, PageCount = v.Pages.Count })
                    .FirstOrDefault(),
            })
            .Where(x => x.LatestPublished != null);

        if (startId is not null)
        {
            query = query.Where(x => x.Id >= startId.Value);
        }

        var page = await query
            .OrderBy(x => x.Id)
            .Take(take + 1)
            .Select(x => new PublishedFormListItem
            {
                Id = x.Id,
                Title = x.LatestPublished!.Title,
                Description = x.LatestPublished.Description,
                VersionNumber = x.LatestPublished.VersionNumber,
                PublishedAt = x.LatestPublished.PublishedAt,
                PageCount = x.LatestPublished.PageCount,
            })
            .ToListAsync(cancellationToken);

        Guid? nextStartId = null;
        if (page.Count > take)
        {
            nextStartId = page[take].Id;
            page.RemoveAt(take);
        }

        return new PublishedFormBatchResult
        {
            Items = page,
            NextStartId = nextStartId,
        };
    }
}

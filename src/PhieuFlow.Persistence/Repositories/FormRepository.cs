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

using PhieuFlow.Hub.Contracts.Publishing;
using PhieuFlow.Hub.Mapping;
using PhieuFlow.Persistence.UnitOfWork;

namespace PhieuFlow.Hub.Endpoints;

// Respondent-facing: published forms only. Its own scope (not forms:read) keeps a
// form-filler token off the draft endpoints, and vice versa.
public static class PublishedFormEndpoints
{
    public static void MapPublishedFormEndpoints(this WebApplication app)
    {
        app.MapGet("/forms/published", async (IUnitOfWork unitOfWork, int take = 20, Guid? startId = null, CancellationToken cancellationToken = default) =>
        {
            if (!TakeParameter.IsValid(take))
            {
                return TakeParameter.OutOfRange();
            }

            var result = await unitOfWork.Forms.GetPublishedBatchAsync(startId, take, cancellationToken);

            return Results.Ok(new PublishedFormBatchResponse
            {
                Items = result.Items.Select(i => new PublishedFormListItemDto
                {
                    Id = i.Id,
                    Title = i.Title,
                    Description = i.Description,
                    VersionNumber = i.VersionNumber,
                    PublishedAt = i.PublishedAt,
                    PageCount = i.PageCount,
                }).ToList(),
                NextStartId = result.NextStartId,
            });
        }).RequireAuthorization("published-forms:read");

        app.MapGet("/forms/published/{id:guid}", async (Guid id, IUnitOfWork unitOfWork, CancellationToken cancellationToken) =>
        {
            var version = await unitOfWork.Forms.GetPublishedByIdAsync(id, cancellationToken);
            return version is null
                ? Results.NotFound()
                : Results.Ok(FormResponseMapper.ToPublishedDto(version));
        }).RequireAuthorization("published-forms:read");
    }
}

using PhieuFlow.Hub.Mapping;
using PhieuFlow.Persistence.UnitOfWork;

namespace PhieuFlow.Hub.Endpoints;

// Builder-facing: read one version's frozen tree by number, for showing form history.
public static class FormVersionEndpoints
{
    public static void MapFormVersionEndpoints(this WebApplication app)
    {
        app.MapGet("/forms/{id:guid}/versions/{versionNumber:int}", async (
            Guid id,
            int versionNumber,
            IUnitOfWork unitOfWork,
            CancellationToken cancellationToken) =>
        {
            var version = await unitOfWork.Forms.GetVersionAsync(id, versionNumber, cancellationToken);
            if (version is null)
            {
                return Results.NotFound();
            }

            var dto = FormResponseMapper.ToDto(version);
            dto.LatestPublishedVersionNumber =
                await unitOfWork.Forms.GetLatestPublishedVersionNumberAsync(id, cancellationToken);
            return Results.Ok(dto);
        }).RequireAuthorization("forms:read");
    }
}

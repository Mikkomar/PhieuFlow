using PhieuFlow.Hub.Contracts.Publishing;
using PhieuFlow.Hub.Contracts.Validation;
using PhieuFlow.Hub.Mapping;
using PhieuFlow.Persistence.Projections;
using PhieuFlow.Persistence.UnitOfWork;

namespace PhieuFlow.Hub.Endpoints;

// The publish gate: validate the persisted latest version, then flip it live.
public static class FormPublishEndpoints
{
    public static void MapFormPublishEndpoints(this WebApplication app)
    {
        // Validate the persisted latest version. Any issue returns 422 with the annotated
        // tree and nothing is published. Callers flush pending edits first.
        app.MapPost("/forms/{id:guid}/publish", async (
            Guid id,
            IUnitOfWork unitOfWork,
            IFormPublishValidator validator,
            CancellationToken cancellationToken) =>
        {
            var version = await unitOfWork.Forms.GetByIdAsync(id, cancellationToken);
            if (version is null)
            {
                return Results.NotFound();
            }

            var liveVersionNumber =
                await unitOfWork.Forms.GetLatestPublishedVersionNumberAsync(id, cancellationToken);

            var dto = FormResponseMapper.ToDto(version);
            dto.LatestPublishedVersionNumber = liveVersionNumber;

            if (!validator.Validate(dto))
            {
                return Results.UnprocessableEntity(new PublishResultDto
                {
                    Published = false,
                    Form = dto,
                    VersionNumber = version.VersionNumber,
                    LiveVersionNumber = liveVersionNumber,
                    IsFirstPublish = liveVersionNumber is null,
                });
            }

            var result = await unitOfWork.Forms.PublishAsync(id, version.VersionNumber, version.Revision, cancellationToken);
            if (result.Status == FormPublishStatus.FormNotFound)
            {
                return Results.NotFound();
            }

            if (result.Status == FormPublishStatus.RevisionMismatch)
            {
                // Another session's save arrived between validate and flip. Nothing was published.
                return Results.Conflict();
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            var state = result.State!;

            // `dto` was mapped before the flip, so only Status is stale. Patch it rather
            // than re-fetching the whole tree, whose content is unchanged.
            dto.Status = FormResponseMapper.ToDto(state.Status);

            return Results.Ok(new PublishResultDto
            {
                Published = true,
                Form = dto,
                VersionNumber = state.VersionNumber,
                LiveVersionNumber = liveVersionNumber,
                IsFirstPublish = liveVersionNumber is null,
                Revision = state.Revision,
                Status = FormResponseMapper.ToDto(state.Status),
                LastModifiedAt = state.LastModifiedAt,
                PublishedAt = state.PublishedAt,
            });
        }).RequireAuthorization("forms:write");
    }
}

using PhieuFlow.Hub.Contracts.Forms;
using PhieuFlow.Hub.Contracts.Publishing;
using PhieuFlow.Hub.Contracts.Submissions;
using PhieuFlow.Hub.Contracts.Validation;
using PhieuFlow.Hub.Mapping;
using PhieuFlow.Persistence.Projections;
using PhieuFlow.Persistence.UnitOfWork;

namespace PhieuFlow.Hub.Endpoints;

public static class FormEndpoints
{
    private const int MaxTake = 100;

    public static void MapFormEndpoints(this WebApplication app)
    {
        app.MapGet("/forms", async (IUnitOfWork unitOfWork, int take = 20, Guid? startId = null, CancellationToken cancellationToken = default) =>
        {
            if (take < 1 || take > MaxTake)
            {
                return Results.BadRequest($"'take' must be between 1 and {MaxTake}.");
            }

            var result = await unitOfWork.Forms.GetBatchAsync(startId, take, cancellationToken);

            return Results.Ok(new FormBatchResponse
            {
                Items = result.Items.Select(i => new FormListItemDto
                {
                    Id = i.Id,
                    Title = i.Title,
                    Description = i.Description,
                    CreatedAt = i.CreatedAt,
                    LastModifiedAt = i.LastModifiedAt,
                    LastModifiedBy = i.LastModifiedBy,
                    Revision = i.Revision,
                    VersionNumber = i.VersionNumber,
                    Status = FormResponseMapper.ToDto(i.Status),
                    LatestPublishedVersionNumber = i.LatestPublishedVersionNumber,
                    LatestPublishedAt = i.LatestPublishedAt,
                    PageCount = i.PageCount,
                    QuestionCount = i.QuestionCount,
                    HasSubmissions = i.HasSubmissions,
                }).ToList(),
                NextStartId = result.NextStartId,
            });
        }).RequireAuthorization("forms:read");

        // Respondent-facing: published forms only. Its own scope (not forms:read) keeps a
        // form-filler token off the draft endpoints above, and vice versa.
        app.MapGet("/forms/published", async (IUnitOfWork unitOfWork, int take = 20, Guid? startId = null, CancellationToken cancellationToken = default) =>
        {
            if (take < 1 || take > MaxTake)
            {
                return Results.BadRequest($"'take' must be between 1 and {MaxTake}.");
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

        app.MapPost("/forms", async (IUnitOfWork unitOfWork, CancellationToken cancellationToken) =>
        {
            var formId = await unitOfWork.Forms.CreateAsync(cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Results.Ok(new FormCreatedDto { Id = formId });
        }).RequireAuthorization("forms:write");

        app.MapGet("/forms/{id:guid}", async (Guid id, IUnitOfWork unitOfWork, CancellationToken cancellationToken) =>
        {
            var version = await unitOfWork.Forms.GetByIdAsync(id, cancellationToken);
            if (version is null)
            {
                return Results.NotFound();
            }

            var dto = FormResponseMapper.ToDto(version);
            dto.LatestPublishedVersionNumber =
                await unitOfWork.Forms.GetLatestPublishedVersionNumberAsync(id, cancellationToken);
            return Results.Ok(dto);
        }).RequireAuthorization("forms:read");

        // Read-back for the FormBuilder Responses view. Keyset-paged by submission id,
        // same contract as GET /forms.
        app.MapGet("/forms/{id:guid}/submissions", async (
            Guid id,
            IUnitOfWork unitOfWork,
            int take = 20,
            Guid? startId = null,
            CancellationToken cancellationToken = default) =>
        {
            if (take < 1 || take > MaxTake)
            {
                return Results.BadRequest($"'take' must be between 1 and {MaxTake}.");
            }

            var result = await unitOfWork.Forms.GetSubmissionsBatchAsync(id, startId, take, cancellationToken);

            return result is null
                ? Results.NotFound()
                : Results.Ok(SubmissionResponseMapper.ToDto(result));
        }).RequireAuthorization("submissions:read");

        app.MapPut("/forms/{id:guid}", async (Guid id, FormDto dto, IUnitOfWork unitOfWork, CancellationToken cancellationToken) =>
        {
            var result = await unitOfWork.Forms.SaveAsync(id, FormRequestMapper.ToEntity(dto, id), cancellationToken);
            if (result.Status == FormSaveStatus.FormNotFound)
            {
                return Results.NotFound();
            }

            if (result.Status == FormSaveStatus.RevisionMismatch)
            {
                // Optimistic concurrency: another session advanced this form. Nothing was written.
                return Results.Conflict();
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            var state = result.State!;
            return Results.Ok(new FormVersionStateDto
            {
                VersionNumber = state.VersionNumber,
                Revision = state.Revision,
                Status = FormResponseMapper.ToDto(state.Status),
                LastModifiedAt = state.LastModifiedAt,
                PublishedAt = state.PublishedAt,
            });
        }).RequireAuthorization("forms:write");

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

        app.MapPost("/forms/{id:guid}/duplicate", async (Guid id, IUnitOfWork unitOfWork, CancellationToken cancellationToken) =>
        {
            var newFormId = await unitOfWork.Forms.DuplicateAsync(id, cancellationToken);
            if (newFormId is null)
            {
                return Results.NotFound();
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Results.Ok(new FormCreatedDto { Id = newFormId.Value });
        }).RequireAuthorization("forms:write");

        app.MapDelete("/forms/{id:guid}", async (Guid id, IUnitOfWork unitOfWork, CancellationToken cancellationToken) =>
        {
            var result = await unitOfWork.Forms.DeleteAsync(id, cancellationToken);
            if (result.Status == FormDeleteStatus.FormNotFound)
            {
                return Results.NotFound();
            }

            if (result.Status == FormDeleteStatus.HasSubmissions)
            {
                // The form has responses with Restrict FKs, so a delete would lose data.
                // The builder disables this too. This covers a stale list or direct call.
                return Results.Conflict();
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        }).RequireAuthorization("forms:write");
    }
}

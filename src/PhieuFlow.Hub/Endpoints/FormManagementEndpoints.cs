using PhieuFlow.Hub.Contracts.Forms;
using PhieuFlow.Hub.Mapping;
using PhieuFlow.Persistence.Projections;
using PhieuFlow.Persistence.UnitOfWork;

namespace PhieuFlow.Hub.Endpoints;

// Builder-facing draft CRUD for forms.
public static class FormManagementEndpoints
{
    public static void MapFormManagementEndpoints(this WebApplication app)
    {
        app.MapGet("/forms", async (IUnitOfWork unitOfWork, int take = 20, Guid? startId = null, CancellationToken cancellationToken = default) =>
        {
            if (!TakeParameter.IsValid(take))
            {
                return TakeParameter.OutOfRange();
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

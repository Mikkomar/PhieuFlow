using PhieuFlow.Core.Entities;
using PhieuFlow.Hub.Contracts;
using PhieuFlow.Hub.Validation;
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
                    Status = MapToDto(i.Status),
                    LatestPublishedVersionNumber = i.LatestPublishedVersionNumber,
                    LatestPublishedAt = i.LatestPublishedAt,
                    PageCount = i.PageCount,
                    QuestionCount = i.QuestionCount,
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

            var dto = MapToDto(version);
            dto.LatestPublishedVersionNumber =
                await unitOfWork.Forms.GetLatestPublishedVersionNumberAsync(id, cancellationToken);
            return Results.Ok(dto);
        }).RequireAuthorization("forms:read");

        app.MapPut("/forms/{id:guid}", async (Guid id, FormDto dto, IUnitOfWork unitOfWork, CancellationToken cancellationToken) =>
        {
            var result = await unitOfWork.Forms.SaveAsync(id, MapToEntity(dto, id), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Results.Ok(new FormVersionStateDto
            {
                VersionNumber = result.VersionNumber,
                Revision = result.Revision,
                Status = MapToDto(result.Status),
                LastModifiedAt = result.LastModifiedAt,
                PublishedAt = result.PublishedAt,
            });
        }).RequireAuthorization("forms:write");

        // Publish gate: validate the persisted latest version — any issue -> 422 with the
        // tree annotated, nothing published. Callers flush pending edits first, so "latest
        // version" is what the builder is showing.
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

            var dto = MapToDto(version);
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

            var state = await unitOfWork.Forms.PublishAsync(id, cancellationToken);
            if (state is null)
            {
                return Results.NotFound();
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Results.Ok(new PublishResultDto
            {
                Published = true,
                Form = dto,
                VersionNumber = state.VersionNumber,
                LiveVersionNumber = liveVersionNumber,
                IsFirstPublish = liveVersionNumber is null,
                Revision = state.Revision,
                Status = MapToDto(state.Status),
                LastModifiedAt = state.LastModifiedAt,
                PublishedAt = state.PublishedAt,
            });
        }).RequireAuthorization("forms:write");
    }

    private static FormVersionStatusDto MapToDto(FormVersionStatus status) => status switch
    {
        FormVersionStatus.Draft => FormVersionStatusDto.Draft,
        FormVersionStatus.Published => FormVersionStatusDto.Published,
        _ => throw new NotSupportedException($"Unknown form version status '{status}'."),
    };

    private static FormDto MapToDto(FormVersion version) => new()
    {
        Id = version.FormId,
        Title = version.Title,
        Description = version.Description,
        CreatedAt = version.CreatedAt,
        LastModifiedAt = version.LastModifiedAt,
        LastModifiedBy = version.LastModifiedBy,
        Revision = version.Revision,
        VersionNumber = version.VersionNumber,
        Status = MapToDto(version.Status),
        Pages = version.Pages.Select(MapToDto).ToList(),
    };

    private static FormPageDto MapToDto(FormPage page) => new()
    {
        Id = page.Id,
        Title = page.Title,
        Questions = page.Questions.Select(MapToDto).ToList(),
    };

    private static QuestionDto MapToDto(Question question) => question switch
    {
        TextAreaQuestion q => new TextAreaQuestionDto
        {
            Id = q.Id,
            Text = q.Text,
            IsRequired = q.IsRequired,
            MinLength = q.MinLength,
            MaxLength = q.MaxLength,
        },
        CheckboxQuestion q => new CheckboxQuestionDto
        {
            Id = q.Id,
            Text = q.Text,
            IsRequired = q.IsRequired,
            Label = q.Label,
        },
        DropDownQuestion q => new DropDownQuestionDto
        {
            Id = q.Id,
            Text = q.Text,
            IsRequired = q.IsRequired,
            Options = MapToDto(q.Options),
        },
        RadioButtonQuestion q => new RadioButtonQuestionDto
        {
            Id = q.Id,
            Text = q.Text,
            IsRequired = q.IsRequired,
            Options = MapToDto(q.Options),
        },
        CheckBoxGroupQuestion q => new CheckBoxGroupQuestionDto
        {
            Id = q.Id,
            Text = q.Text,
            IsRequired = q.IsRequired,
            Options = MapToDto(q.Options),
            MinSelections = q.MinSelections,
            MaxSelections = q.MaxSelections,
        },
        NumberQuestion q => new NumberQuestionDto
        {
            Id = q.Id,
            Text = q.Text,
            IsRequired = q.IsRequired,
            Min = q.Min,
            Max = q.Max,
        },
        CalendarQuestion q => new CalendarQuestionDto
        {
            Id = q.Id,
            Text = q.Text,
            IsRequired = q.IsRequired,
            MinDate = q.MinDate,
            MaxDate = q.MaxDate,
        },
        _ => throw new NotSupportedException($"Unknown question type '{question.GetType().Name}'."),
    };

    private static List<QuestionOptionDto> MapToDto(IEnumerable<QuestionOption> options) => options
        .OrderBy(o => o.Order)
        .Select(o => new QuestionOptionDto { Id = o.Id, Label = o.Label, Order = o.Order })
        .ToList();

    private static FormVersion MapToEntity(FormDto dto, Guid formId) => new()
    {
        Id = Guid.NewGuid(),
        FormId = formId,
        Title = dto.Title,
        Description = dto.Description,
        // TODO ADR-0005: the calling client id is now available via User.FindFirst("azp").
        // End-user identity / per-user ownership is out of ADR 0005 scope, so this stays
        // free text from the DTO for now.
        LastModifiedBy = dto.LastModifiedBy,
        Pages = dto.Pages.Select((p, index) => MapToEntity(p, formId, index)).ToList(),
    };

    private static FormPage MapToEntity(FormPageDto dto, Guid formVersionId, int order) => new()
    {
        Id = dto.Id,
        FormVersionId = formVersionId,
        Title = dto.Title,
        Order = order,
        Questions = dto.Questions.Select((q, index) => MapToEntity(q, dto.Id, index)).ToList(),
    };

    private static Question MapToEntity(QuestionDto dto, Guid formPageId, int order) => dto switch
    {
        TextAreaQuestionDto q => new TextAreaQuestion
        {
            Id = q.Id,
            FormPageId = formPageId,
            Text = q.Text,
            IsRequired = q.IsRequired,
            Order = order,
            MinLength = q.MinLength,
            MaxLength = q.MaxLength,
        },
        CheckboxQuestionDto q => new CheckboxQuestion
        {
            Id = q.Id,
            FormPageId = formPageId,
            Text = q.Text,
            IsRequired = q.IsRequired,
            Order = order,
            Label = q.Label,
        },
        DropDownQuestionDto q => new DropDownQuestion
        {
            Id = q.Id,
            FormPageId = formPageId,
            Text = q.Text,
            IsRequired = q.IsRequired,
            Order = order,
            Options = MapToEntity(q.Options),
        },
        RadioButtonQuestionDto q => new RadioButtonQuestion
        {
            Id = q.Id,
            FormPageId = formPageId,
            Text = q.Text,
            IsRequired = q.IsRequired,
            Order = order,
            Options = MapToEntity(q.Options),
        },
        CheckBoxGroupQuestionDto q => new CheckBoxGroupQuestion
        {
            Id = q.Id,
            FormPageId = formPageId,
            Text = q.Text,
            IsRequired = q.IsRequired,
            Order = order,
            Options = MapToEntity(q.Options),
            MinSelections = q.MinSelections,
            MaxSelections = q.MaxSelections,
        },
        NumberQuestionDto q => new NumberQuestion
        {
            Id = q.Id,
            FormPageId = formPageId,
            Text = q.Text,
            IsRequired = q.IsRequired,
            Order = order,
            Min = q.Min,
            Max = q.Max,
        },
        CalendarQuestionDto q => new CalendarQuestion
        {
            Id = q.Id,
            FormPageId = formPageId,
            Text = q.Text,
            IsRequired = q.IsRequired,
            Order = order,
            MinDate = q.MinDate,
            MaxDate = q.MaxDate,
        },
        _ => throw new NotSupportedException($"Unknown question type '{dto.GetType().Name}'."),
    };

    // Option order is authoritative from the incoming list position, mirroring how pages
    // and questions get their Order.
    private static List<QuestionOption> MapToEntity(IEnumerable<QuestionOptionDto> options) => options
        .Select((o, index) => new QuestionOption { Id = o.Id, Label = o.Label, Order = index })
        .ToList();
}

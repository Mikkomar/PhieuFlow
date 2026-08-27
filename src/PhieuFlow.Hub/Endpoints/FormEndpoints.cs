using PhieuFlow.Core.Entities;
using PhieuFlow.Hub.Contracts;
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
                    PageCount = i.PageCount,
                    QuestionCount = i.QuestionCount,
                }).ToList(),
                NextStartId = result.NextStartId,
            });
        });

        app.MapGet("/forms/{id:guid}", async (Guid id, IUnitOfWork unitOfWork, CancellationToken cancellationToken) =>
        {
            var form = await unitOfWork.Forms.GetByIdAsync(id, cancellationToken);
            return form is null ? Results.NotFound() : Results.Ok(MapToDto(form));
        });
    }

    private static FormDto MapToDto(Form form) => new()
    {
        Id = form.Id,
        Title = form.Title,
        Description = form.Description,
        CreatedAt = form.CreatedAt,
        LastModifiedAt = form.LastModifiedAt,
        LastModifiedBy = form.LastModifiedBy,
        Revision = form.Revision,
        Pages = form.Pages.Select(MapToDto).ToList(),
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
        .Select(o => new QuestionOptionDto { Id = o.Id, Label = o.Label })
        .ToList();
}

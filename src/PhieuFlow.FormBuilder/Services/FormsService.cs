using PhieuFlow.Core.Entities;
using PhieuFlow.FormBuilder.Models;
using PhieuFlow.Hub.Contracts;

namespace PhieuFlow.FormBuilder.Services;

public class FormsService(HubFormsClient hubFormsClient) : IFormsService
{
    public async Task<List<FormSummary>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var dtos = await hubFormsClient.GetAllFormsAsync(cancellationToken);
        return dtos.Select(dto => new FormSummary
        {
            Id = dto.Id,
            Title = dto.Title,
            Description = dto.Description,
            Status = MapToModel(dto.Status),
            CreatedAt = dto.CreatedAt,
            LastModifiedAt = dto.LastModifiedAt,
            LastModifiedBy = dto.LastModifiedBy ?? string.Empty,
            Revision = dto.Revision,
            VersionNumber = dto.VersionNumber,
            LatestPublishedVersionNumber = dto.LatestPublishedVersionNumber,
            LatestPublishedAt = dto.LatestPublishedAt,
            QuestionCount = dto.QuestionCount,
            PageCount = dto.PageCount,
        }).ToList();
    }

    public async Task<FormVersion?> GetByIdAsync(Guid formId, CancellationToken cancellationToken = default)
    {
        var dto = await hubFormsClient.GetFormByIdAsync(formId, cancellationToken);
        return dto is null ? null : MapToEntity(dto);
    }

    public async Task<FormVersionStateDto> SaveAsync(Guid formId, FormVersion form, CancellationToken cancellationToken = default)
    {
        return await hubFormsClient.SaveFormAsync(MapToDto(formId, form), cancellationToken);
    }

    public async Task<FormVersionStateDto> PublishAsync(Guid formId, CancellationToken cancellationToken = default)
    {
        return await hubFormsClient.PublishFormAsync(formId, cancellationToken);
    }

    private static FormStatus MapToModel(FormVersionStatusDto status) => status switch
    {
        FormVersionStatusDto.Draft => FormStatus.Draft,
        FormVersionStatusDto.Published => FormStatus.Published,
        _ => throw new NotSupportedException($"Unknown form version status '{status}'."),
    };

    private static FormVersionStatusDto MapToDto(FormVersionStatus status) => status switch
    {
        FormVersionStatus.Draft => FormVersionStatusDto.Draft,
        FormVersionStatus.Published => FormVersionStatusDto.Published,
        _ => throw new NotSupportedException($"Unknown form version status '{status}'."),
    };

    private static FormVersionStatus MapToEntity(FormVersionStatusDto status) => status switch
    {
        FormVersionStatusDto.Draft => FormVersionStatus.Draft,
        FormVersionStatusDto.Published => FormVersionStatus.Published,
        _ => throw new NotSupportedException($"Unknown form version status '{status}'."),
    };

    private static FormDto MapToDto(Guid formId, FormVersion form) => new()
    {
        Id = formId,
        Title = form.Title,
        Description = form.Description,
        CreatedAt = form.CreatedAt,
        LastModifiedAt = form.LastModifiedAt,
        LastModifiedBy = form.LastModifiedBy,
        Revision = form.Revision,
        VersionNumber = form.VersionNumber,
        Status = MapToDto(form.Status),
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

    private static FormVersion MapToEntity(FormDto dto) => new()
    {
        Id = Guid.NewGuid(),
        FormId = dto.Id,
        Title = dto.Title,
        Description = dto.Description,
        CreatedAt = dto.CreatedAt,
        LastModifiedAt = dto.LastModifiedAt,
        LastModifiedBy = dto.LastModifiedBy,
        Revision = dto.Revision,
        VersionNumber = dto.VersionNumber,
        Status = MapToEntity(dto.Status),
        Pages = dto.Pages.Select(p => MapToEntity(p, dto.Id)).ToList(),
    };

    private static FormPage MapToEntity(FormPageDto dto, Guid formVersionId) => new()
    {
        Id = dto.Id,
        FormVersionId = formVersionId,
        Title = dto.Title,
        Questions = dto.Questions.Select(q => MapToEntity(q, dto.Id)).ToList(),
    };

    private static Question MapToEntity(QuestionDto dto, Guid formPageId) => dto switch
    {
        TextAreaQuestionDto q => new TextAreaQuestion
        {
            Id = q.Id,
            FormPageId = formPageId,
            Text = q.Text,
            IsRequired = q.IsRequired,
            MinLength = q.MinLength,
            MaxLength = q.MaxLength,
        },
        CheckboxQuestionDto q => new CheckboxQuestion
        {
            Id = q.Id,
            FormPageId = formPageId,
            Text = q.Text,
            IsRequired = q.IsRequired,
            Label = q.Label,
        },
        DropDownQuestionDto q => new DropDownQuestion
        {
            Id = q.Id,
            FormPageId = formPageId,
            Text = q.Text,
            IsRequired = q.IsRequired,
            Options = MapToEntity(q.Options),
        },
        RadioButtonQuestionDto q => new RadioButtonQuestion
        {
            Id = q.Id,
            FormPageId = formPageId,
            Text = q.Text,
            IsRequired = q.IsRequired,
            Options = MapToEntity(q.Options),
        },
        CheckBoxGroupQuestionDto q => new CheckBoxGroupQuestion
        {
            Id = q.Id,
            FormPageId = formPageId,
            Text = q.Text,
            IsRequired = q.IsRequired,
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
            Min = q.Min,
            Max = q.Max,
        },
        CalendarQuestionDto q => new CalendarQuestion
        {
            Id = q.Id,
            FormPageId = formPageId,
            Text = q.Text,
            IsRequired = q.IsRequired,
            MinDate = q.MinDate,
            MaxDate = q.MaxDate,
        },
        _ => throw new NotSupportedException($"Unknown question type '{dto.GetType().Name}'."),
    };

    private static List<QuestionOption> MapToEntity(IEnumerable<QuestionOptionDto> options) => options
        .Select(o => new QuestionOption { Id = o.Id, Label = o.Label })
        .ToList();
}

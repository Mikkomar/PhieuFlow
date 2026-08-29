using PhieuFlow.Core.Entities;
using PhieuFlow.FormBuilder.Clients;
using PhieuFlow.FormBuilder.Models;
using PhieuFlow.FormBuilder.Models.Editing;
using PhieuFlow.Hub.Contracts;

namespace PhieuFlow.FormBuilder.Services;

public class FormsService(IHubFormsClient hubFormsClient) : IFormsService
{
    public Task<Guid> CreateNewAsync(CancellationToken cancellationToken = default) =>
        hubFormsClient.CreateFormAsync(cancellationToken);

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

    public async Task<FormEditModel?> GetByIdAsync(Guid formId, CancellationToken cancellationToken = default)
    {
        var dto = await hubFormsClient.GetFormByIdAsync(formId, cancellationToken);
        return dto is null ? null : FormEditMapper.ToEditModel(dto);
    }

    public Task<FormVersionStateDto> SaveAsync(FormEditModel form, CancellationToken cancellationToken = default) =>
        hubFormsClient.SaveFormAsync(FormEditMapper.ToDto(form), cancellationToken);

    public Task<PublishResultDto> PublishAsync(Guid formId, CancellationToken cancellationToken = default) =>
        hubFormsClient.PublishFormAsync(formId, cancellationToken);

    public Task DeleteAsync(Guid formId, CancellationToken cancellationToken = default) =>
        hubFormsClient.DeleteFormAsync(formId, cancellationToken);

    public async Task<Guid> DuplicateAsync(Guid sourceId, CancellationToken cancellationToken = default)
    {
        var source = await GetByIdAsync(sourceId, cancellationToken)
            ?? throw new InvalidOperationException($"Form {sourceId} no longer exists.");

        var newId = await CreateNewAsync(cancellationToken);

        var copy = FormEditMapper.ToEditModel(FormEditMapper.ToDto(source));
        copy.FormId = newId;
        copy.Status = FormVersionStatus.Draft;
        copy.LiveVersionNumber = null;
        copy.Title = string.IsNullOrWhiteSpace(source.Title) ? "Copy of untitled form" : $"Copy of {source.Title}";
        RefreshIds(copy);

        await SaveAsync(copy, cancellationToken);
        return newId;
    }

    /// <summary>Assigns fresh ids to every page, question and option so the copy shares nothing with its source.</summary>
    private static void RefreshIds(FormEditModel form)
    {
        foreach (var page in form.Pages)
        {
            page.Id = Guid.NewGuid();
            foreach (var question in page.Questions)
            {
                question.Id = Guid.NewGuid();
                if (question is ChoiceQuestionEditModel choice)
                {
                    foreach (var option in choice.Options)
                    {
                        option.Id = Guid.NewGuid();
                    }
                }
            }
        }
    }

    private static FormStatus MapToModel(FormVersionStatusDto status) => status switch
    {
        FormVersionStatusDto.Draft => FormStatus.Draft,
        FormVersionStatusDto.Published => FormStatus.Published,
        _ => throw new NotSupportedException($"Unknown form version status '{status}'."),
    };
}

using System.Runtime.CompilerServices;
using PhieuFlow.FormBuilder.Clients;
using PhieuFlow.FormBuilder.Models;
using PhieuFlow.FormBuilder.Models.Editing;
using PhieuFlow.Hub.Contracts;

namespace PhieuFlow.FormBuilder.Services;

public class FormsService(IHubFormsClient hubFormsClient) : IFormsService
{
    public Task<Guid> CreateNewAsync(CancellationToken cancellationToken = default) =>
        hubFormsClient.CreateFormAsync(cancellationToken);

    public async IAsyncEnumerable<List<FormSummary>> GetAllStreamingAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var batch in hubFormsClient.GetFormBatchesAsync(cancellationToken))
        {
            yield return batch.Select(dto => new FormSummary
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

    public async Task<IReadOnlyDictionary<Guid, Guid>> ReconcileForkAsync(
        FormEditModel local, CancellationToken cancellationToken = default)
    {
        FormEditModel? forked;
        try
        {
            forked = await GetByIdAsync(local.FormId, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return EmptyRemap;
        }

        if (forked is null)
        {
            return EmptyRemap;
        }

        // The server's fork is a structure-preserving deep clone, so match by position and copy
        // only the ids.
        local.LiveVersionNumber = forked.LiveVersionNumber;

        var pageIdRemap = new Dictionary<Guid, Guid>();
        foreach (var (localPage, forkedPage) in local.Pages.Zip(forked.Pages))
        {
            pageIdRemap[localPage.Id] = forkedPage.Id;
            localPage.Id = forkedPage.Id;

            foreach (var (localQuestion, forkedQuestion) in localPage.Questions.Zip(forkedPage.Questions))
            {
                localQuestion.Id = forkedQuestion.Id;

                if (localQuestion is ChoiceQuestionEditModel localChoice
                    && forkedQuestion is ChoiceQuestionEditModel forkedChoice)
                {
                    foreach (var (localOption, forkedOption) in localChoice.Options.Zip(forkedChoice.Options))
                    {
                        localOption.Id = forkedOption.Id;
                    }
                }
            }
        }

        return pageIdRemap;
    }

    private static readonly IReadOnlyDictionary<Guid, Guid> EmptyRemap = new Dictionary<Guid, Guid>();

    public Task DeleteAsync(Guid formId, CancellationToken cancellationToken = default) =>
        hubFormsClient.DeleteFormAsync(formId, cancellationToken);

    public Task<Guid> DuplicateAsync(Guid sourceId, CancellationToken cancellationToken = default) =>
        hubFormsClient.DuplicateFormAsync(sourceId, cancellationToken);

    private static FormStatus MapToModel(FormVersionStatusDto status) => status switch
    {
        FormVersionStatusDto.Draft => FormStatus.Draft,
        FormVersionStatusDto.Published => FormStatus.Published,
        _ => throw new NotSupportedException($"Unknown form version status '{status}'."),
    };
}

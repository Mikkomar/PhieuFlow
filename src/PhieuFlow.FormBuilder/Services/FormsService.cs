using PhieuFlow.FormBuilder.Models;

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
            Status = FormStatus.Draft,
            CreatedAt = dto.CreatedAt,
            LastModifiedAt = dto.LastModifiedAt,
            LastModifiedBy = dto.LastModifiedBy ?? string.Empty,
            Revision = dto.Revision,
            QuestionCount = dto.QuestionCount,
            PageCount = dto.PageCount,
        }).ToList();
    }
}

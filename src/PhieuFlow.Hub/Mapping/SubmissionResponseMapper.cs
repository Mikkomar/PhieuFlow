using PhieuFlow.Hub.Contracts.Submissions;
using PhieuFlow.Persistence.Projections;

namespace PhieuFlow.Hub.Mapping;

/// <summary>Persistence read model -> wire DTO for the FormBuilder Responses view.</summary>
public static class SubmissionResponseMapper
{
    public static SubmissionBatchResponse ToDto(SubmissionBatchResult result) => new()
    {
        Items = result.Items.Select(ToDto).ToList(),
        NextStartId = result.NextStartId,
    };

    private static SubmissionListItemDto ToDto(SubmissionListItem item) => new()
    {
        Id = item.Id,
        SubmittedAt = item.SubmittedAt,
        FormVersionNumber = item.FormVersionNumber,
        Answers = item.Answers.Select(a => new SubmissionAnswerValueDto
        {
            QuestionId = a.QuestionId,
            QuestionText = a.QuestionText,
            Order = a.Order,
            Value = a.Value,
        }).ToList(),
    };
}

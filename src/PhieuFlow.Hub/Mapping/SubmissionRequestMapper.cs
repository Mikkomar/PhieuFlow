using PhieuFlow.Core.Entities;
using PhieuFlow.Hub.Contracts.Submissions;

namespace PhieuFlow.Hub.Mapping;

/// <summary>
/// Type switch from an inbound <see cref="SubmissionAnswerDto"/> to its matching
/// <see cref="SubmissionAnswer"/>. A CheckBoxGroup selection arrives as one
/// <see cref="OptionAnswerDto"/> per chosen option.
/// </summary>
internal static class SubmissionRequestMapper
{
    public static SubmissionAnswer ToEntity(SubmissionAnswerDto dto, Guid submissionId) => dto switch
    {
        ValueAnswerDto v => new ValueSubmissionAnswer
        {
            Id = Guid.NewGuid(),
            FormSubmissionId = submissionId,
            QuestionId = v.QuestionId,
            QuestionText = v.QuestionText,
            Order = v.Order,
            Value = v.Value,
        },
        BooleanAnswerDto b => new BooleanSubmissionAnswer
        {
            Id = Guid.NewGuid(),
            FormSubmissionId = submissionId,
            QuestionId = b.QuestionId,
            QuestionText = b.QuestionText,
            Order = b.Order,
            Checked = b.Checked,
        },
        OptionAnswerDto o => new OptionSubmissionAnswer
        {
            Id = Guid.NewGuid(),
            FormSubmissionId = submissionId,
            QuestionId = o.QuestionId,
            QuestionText = o.QuestionText,
            Order = o.Order,
            OptionId = o.OptionId,
        },
        _ => throw new NotSupportedException($"Unknown submission answer DTO '{dto.GetType().Name}'."),
    };
}

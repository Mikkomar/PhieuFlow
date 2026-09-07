using PhieuFlow.Core.Entities;
using PhieuFlow.Hub.Contracts.Submissions;

namespace PhieuFlow.Hub.Mapping;

/// <summary>
/// Maps an inbound <see cref="SubmissionAnswerDto"/> onto its <see cref="SubmissionAnswer"/>
/// entity. The DTO hierarchy mirrors the entity hierarchy 1:1, so this is a straight type
/// switch — the reverse of the form-filler's <c>FillPage.BuildAnswers</c>. A CheckBoxGroup
/// selection already arrives as one <see cref="OptionAnswerDto"/> per chosen option, so no
/// per-question-type fan-out is needed here.
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

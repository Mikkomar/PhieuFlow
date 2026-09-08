using System.Text.Json.Serialization;

namespace PhieuFlow.Hub.Contracts.Submissions;

/// <summary>
/// A completed form response sent to the Hub over the async queue. Shared by the
/// form-filler publisher and the Hub consumer. <see cref="FormVersionNumber"/> names the
/// published version the respondent filled.
/// </summary>
public class FormSubmissionRequest
{
    public required Guid FormId { get; set; }
    public required int FormVersionNumber { get; set; }
    public required List<SubmissionAnswerDto> Answers { get; set; }
}

/// <summary>
/// One answer. Option answers carry only <see cref="OptionAnswerDto.OptionId"/>. The label
/// resolves from the published form, which never changes.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ValueAnswerDto), "Value")]
[JsonDerivedType(typeof(BooleanAnswerDto), "Boolean")]
[JsonDerivedType(typeof(OptionAnswerDto), "Option")]
public abstract class SubmissionAnswerDto
{
    public required Guid QuestionId { get; set; }
    public required string QuestionText { get; set; }
    public int Order { get; set; }
}

/// <summary>TextArea, Number, or Calendar: the raw input string.</summary>
public class ValueAnswerDto : SubmissionAnswerDto
{
    public string? Value { get; set; }
}

/// <summary>A single yes/no checkbox question.</summary>
public class BooleanAnswerDto : SubmissionAnswerDto
{
    public bool Checked { get; set; }
}

/// <summary>One chosen option of a choice question.</summary>
public class OptionAnswerDto : SubmissionAnswerDto
{
    public required Guid OptionId { get; set; }
}

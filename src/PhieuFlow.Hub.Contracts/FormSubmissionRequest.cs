using System.Text.Json.Serialization;

namespace PhieuFlow.Hub.Contracts;

/// <summary>
/// A completed form response on its way to the Hub. The transport is async RabbitMQ
/// (ADR 0001); this contract is shared by the form-filler's publisher and the future Hub
/// consumer. <see cref="FormVersionNumber"/> identifies the published version the
/// respondent filled — the consumer resolves it to a version id via the unique
/// <c>FormVersions(FormId, VersionNumber)</c> index.
/// </summary>
public class FormSubmissionRequest
{
    public required Guid FormId { get; set; }
    public required int FormVersionNumber { get; set; }
    public required List<SubmissionAnswerDto> Answers { get; set; }
}

/// <summary>
/// One answer. Option-based answers carry only <see cref="OptionAnswerDto.OptionId"/> — the
/// label lives on the form definition, and the published version is immutable (ADR 0007) so
/// it always resolves.
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

/// <summary>TextArea, Number, Calendar — the raw input string.</summary>
public class ValueAnswerDto : SubmissionAnswerDto
{
    public string? Value { get; set; }
}

/// <summary>A single yes/no checkbox question.</summary>
public class BooleanAnswerDto : SubmissionAnswerDto
{
    public bool Checked { get; set; }
}

/// <summary>One chosen option of a DropDown / RadioButton / CheckBoxGroup question.</summary>
public class OptionAnswerDto : SubmissionAnswerDto
{
    public required Guid OptionId { get; set; }
}

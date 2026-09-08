using System.Text.Json.Serialization;

namespace PhieuFlow.Hub.Contracts.Validation;

/// <summary>
/// The control a <see cref="ValidationIssueDto"/> points to, for highlighting and jump
/// links. <see cref="None"/> means the whole node (for example, a page with no questions).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ValidationField
{
    None,
    Title,
    Text,
    Label,
    Options,
    Min,
    Max,
    MinSelections,
}

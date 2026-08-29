using System.Text.Json.Serialization;

namespace PhieuFlow.Hub.Contracts;

/// <summary>
/// Names the control a <see cref="ValidationIssueDto"/> points at, so the builder can
/// highlight it and a jump link can focus it. <see cref="None"/> means the issue belongs to
/// the node as a whole (e.g. a page with no questions).
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

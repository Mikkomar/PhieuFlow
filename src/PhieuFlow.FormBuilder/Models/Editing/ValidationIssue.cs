using PhieuFlow.Hub.Contracts.Validation;

namespace PhieuFlow.FormBuilder.Models.Editing;

/// <summary>
/// A publish-blocking problem the Hub validator attached to this node, for inline display.
/// Mirrors <see cref="ValidationIssueDto"/>.
/// </summary>
public sealed record ValidationIssue(string Message, ValidationField Field);

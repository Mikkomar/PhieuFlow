using PhieuFlow.Hub.Contracts;

namespace PhieuFlow.FormBuilder.Models.Editing;

/// <summary>
/// A publish-blocking problem the Hub validator hung on this node, ready for the builder to
/// render inline. Mirrors <see cref="ValidationIssueDto"/>.
/// </summary>
public sealed record ValidationIssue(string Message, ValidationField Field);

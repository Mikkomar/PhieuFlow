namespace PhieuFlow.FormBuilder.Models.Editing;

/// <summary>
/// Where a pre-publish jump link points. Both ids null means the form title. The card
/// resolves the exact field within a question from its own <c>Issues</c>.
/// </summary>
public sealed record JumpTarget(Guid? PageId, Guid? QuestionId);

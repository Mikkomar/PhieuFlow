namespace PhieuFlow.FormBuilder.Models.Editing;

/// <summary>
/// Where a pre-publish dialog jump link points. Both ids null means the form title. The offending
/// field within a question is resolved by the card from its own <c>Issues</c>.
/// </summary>
public sealed record JumpTarget(Guid? PageId, Guid? QuestionId);

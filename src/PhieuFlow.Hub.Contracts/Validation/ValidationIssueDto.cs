namespace PhieuFlow.Hub.Contracts.Validation;

/// <summary>
/// One publish-blocking problem, attached to the tree node it concerns. Position in the
/// tree is the location, so it carries no ids.
/// </summary>
public class ValidationIssueDto
{
    public required string Message { get; set; }
    public required ValidationField Field { get; set; }
}

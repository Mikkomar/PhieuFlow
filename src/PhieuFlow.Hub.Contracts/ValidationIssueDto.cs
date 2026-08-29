namespace PhieuFlow.Hub.Contracts;

/// <summary>
/// One publish-blocking problem, attached to the tree node it concerns. The node's position
/// in the form tree is the location, so the issue carries no ids.
/// </summary>
public class ValidationIssueDto
{
    public required string Message { get; set; }
    public required ValidationField Field { get; set; }
}

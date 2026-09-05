namespace PhieuFlow.FormBuilder.Models.Editing;

/// <summary>An edit-tree node that carries its own <see cref="ValidationIssue"/> list.</summary>
public interface IHasIssues
{
    List<ValidationIssue> Issues { get; }
}

public static class HasIssuesExtensions
{
    /// <summary>Applies <paramref name="mutate"/> and clears this node's own issues — this node
    /// changed, so whatever the Hub validator said about it no longer applies. Every other node's
    /// issues are untouched.</summary>
    public static void Edit(this IHasIssues node, Action mutate)
    {
        mutate();
        node.Issues.Clear();
    }
}

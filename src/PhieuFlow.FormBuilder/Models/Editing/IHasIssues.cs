namespace PhieuFlow.FormBuilder.Models.Editing;

/// <summary>An edit-tree node that carries its own <see cref="ValidationIssue"/> list.</summary>
public interface IHasIssues
{
    List<ValidationIssue> Issues { get; }
}

public static class HasIssuesExtensions
{
    /// <summary>Applies <paramref name="mutate"/> and clears this node's own issues, since it
    /// changed. Other nodes' issues are untouched.</summary>
    public static void Edit(this IHasIssues node, Action mutate)
    {
        mutate();
        node.Issues.Clear();
    }
}

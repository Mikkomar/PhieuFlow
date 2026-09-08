using PhieuFlow.Core.Entities;

namespace PhieuFlow.Persistence.Reconciliation;

/// <summary>
/// Deep-copies a page/question/option tree with fresh ids for every node. Used when a new
/// tree must be inserted, not reconciled: forking a published version on edit, and
/// duplicating a form.
/// </summary>
public interface IFormTreeCloner
{
    /// <summary>
    /// Returns a copy of <paramref name="source"/> and its whole subtree with new ids, parented
    /// to <paramref name="newVersionId"/>. Throws <see cref="NotSupportedException"/> if the tree
    /// carries an unknown question subtype.
    /// </summary>
    FormPage ClonePageWithFreshIds(FormPage source, Guid newVersionId);
}

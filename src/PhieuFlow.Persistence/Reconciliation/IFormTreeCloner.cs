using PhieuFlow.Core.Entities;

namespace PhieuFlow.Persistence.Reconciliation;

/// <summary>
/// Deep-copies a page/question/option tree, minting fresh ids for every node. Used when a
/// brand-new tree has to be inserted rather than reconciled into an existing one: forking a
/// published version on edit (ADR 0007) and duplicating a form.
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

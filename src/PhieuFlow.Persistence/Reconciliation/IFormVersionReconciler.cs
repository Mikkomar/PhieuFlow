using PhieuFlow.Core.Entities;

namespace PhieuFlow.Persistence.Reconciliation;

/// <summary>
/// Applies an incoming form edit to the current version following the versioning policy in
/// ADR 0007: a <see cref="FormVersionStatus.Draft"/> version is reconciled in place and its
/// <see cref="FormVersion.Revision"/> bumped; a <see cref="FormVersionStatus.Published"/> version
/// is immutable, so the edit forks a new draft (next <see cref="FormVersion.VersionNumber"/>,
/// <see cref="FormVersion.Revision"/> reset to 1, the whole tree deep-cloned with fresh ids).
/// Pure in-memory graph work — never touches the database; the caller loads and persists.
/// </summary>
public interface IFormVersionReconciler
{
    /// <summary>
    /// Reconciles <paramref name="incomingContent"/> onto <paramref name="currentVersion"/>.
    /// </summary>
    /// <param name="currentVersion">
    /// The latest version for the form, loaded and change-tracked with its full page tree.
    /// </param>
    /// <param name="incomingContent">The edit as submitted by the client.</param>
    /// <param name="timestamp">The single timestamp to stamp on the touched/created rows.</param>
    /// <exception cref="InvalidOperationException">A matched question changed its type.</exception>
    /// <exception cref="NotSupportedException">The tree carries an unknown question subtype.</exception>
    FormVersionReconcileResult Reconcile(
        FormVersion currentVersion, FormVersion incomingContent, DateTimeOffset timestamp);
}

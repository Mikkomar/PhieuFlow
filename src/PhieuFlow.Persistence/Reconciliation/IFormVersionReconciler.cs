using PhieuFlow.Core.Entities;

namespace PhieuFlow.Persistence.Reconciliation;

/// <summary>
/// Applies an incoming form edit to the current version. A Draft version is reconciled in
/// place with its Revision bumped. A Published version cannot change, so the edit forks a
/// new draft. Pure in-memory work. The caller loads and persists.
/// </summary>
public interface IFormVersionReconciler
{
    /// <summary>
    /// Reconciles <paramref name="incomingContent"/> onto <paramref name="currentVersion"/>.
    /// </summary>
    /// <param name="currentVersion">The latest version, change-tracked with its full page tree.</param>
    /// <param name="incomingContent">The edit as submitted by the client.</param>
    /// <param name="timestamp">The single timestamp to stamp on changed or created rows.</param>
    /// <exception cref="InvalidOperationException">A matched question changed its type.</exception>
    /// <exception cref="NotSupportedException">The tree carries an unknown question subtype.</exception>
    FormVersionReconcileResult Reconcile(
        FormVersion currentVersion, FormVersion incomingContent, DateTimeOffset timestamp);
}

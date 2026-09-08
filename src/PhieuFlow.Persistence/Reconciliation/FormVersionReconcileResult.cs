using PhieuFlow.Core.Entities;

namespace PhieuFlow.Persistence.Reconciliation;

/// <summary>
/// Outcome of <see cref="IFormVersionReconciler.Reconcile"/>: the version to persist, and
/// whether it is a new forked row (the caller must <c>Add</c> it) or the tracked draft
/// changed in place.
/// </summary>
public readonly record struct FormVersionReconcileResult(FormVersion Version, bool IsFork)
{
    public static FormVersionReconcileResult UpdatedInPlace(FormVersion version) => new(version, false);

    public static FormVersionReconcileResult Forked(FormVersion version) => new(version, true);
}

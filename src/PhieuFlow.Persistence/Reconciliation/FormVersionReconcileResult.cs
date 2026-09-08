using PhieuFlow.Core.Entities;

namespace PhieuFlow.Persistence.Reconciliation;

/// <summary>
/// Outcome of <see cref="IFormVersionReconciler.Reconcile"/>: the version the caller should
/// persist, and whether it is a freshly forked row (which the caller must <c>Add</c>) or the
/// existing draft mutated in place (already tracked).
/// </summary>
public readonly record struct FormVersionReconcileResult(FormVersion Version, bool IsFork)
{
    public static FormVersionReconcileResult UpdatedInPlace(FormVersion version) => new(version, false);

    public static FormVersionReconcileResult Forked(FormVersion version) => new(version, true);
}

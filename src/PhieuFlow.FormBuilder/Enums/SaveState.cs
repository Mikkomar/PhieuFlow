namespace PhieuFlow.FormBuilder.Enums;

/// <summary>
/// Lifecycle of the form builder's debounced autosave, surfaced in the header's
/// save indicator. Shared by <c>FormBuilder</c> (owns the state machine) and
/// <c>FormBuilderHeader</c> (renders it).
/// </summary>
public enum SaveState
{
    Idle,
    Pending,
    Saving,
    Saved,
    Error,
}

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

    /// <summary>The last save failed to reach the server. Retrying may still succeed.</summary>
    Error,

    /// <summary>
    /// The server rejected the save with a 409 — another session advanced this form. Terminal:
    /// autosave stops retrying and the header offers a reload (a plain retry can never clear it).
    /// </summary>
    Conflict,
}

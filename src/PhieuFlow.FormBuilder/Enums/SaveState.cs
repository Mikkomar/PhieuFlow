namespace PhieuFlow.FormBuilder.Enums;

/// <summary>
/// Lifecycle of the builder's debounced autosave, shown in the header's save indicator.
/// Shared by <c>FormBuilder</c> and <c>FormBuilderHeader</c>.
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
    /// The server rejected the save with a 409: another session advanced this form. Terminal.
    /// Autosave stops retrying and the header offers a reload.
    /// </summary>
    Conflict,
}

namespace PhieuFlow.FormBuilder.Clients;

/// <summary>
/// Thrown by <see cref="HubFormsClient.SaveFormAsync"/> when the Hub answers a save with
/// <c>409 Conflict</c> — the form was advanced by another session, so this tab's copy is stale
/// and the save was refused. The autosave controller turns this into a terminal conflict state.
/// </summary>
public sealed class FormRevisionConflictException(Guid formId)
    : Exception($"Form {formId} was changed by another session; this save was rejected.")
{
    public Guid FormId { get; } = formId;
}

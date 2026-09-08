namespace PhieuFlow.FormBuilder.Clients;

/// <summary>
/// Thrown by <see cref="HubFormsClient.SaveFormAsync"/> on a <c>409 Conflict</c>: another
/// session advanced the form, so this tab's copy is stale. The autosave controller treats
/// this as a terminal conflict.
/// </summary>
public sealed class FormRevisionConflictException(Guid formId)
    : Exception($"Form {formId} was changed by another session; this save was rejected.")
{
    public Guid FormId { get; } = formId;
}

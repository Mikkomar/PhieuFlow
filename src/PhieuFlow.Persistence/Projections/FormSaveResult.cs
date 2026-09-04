namespace PhieuFlow.Persistence.Projections;

/// <summary>The three outcomes of <see cref="Repositories.IFormRepository.SaveAsync"/>.</summary>
public enum FormSaveStatus
{
    /// <summary>The incoming content was applied (or forked); <see cref="FormSaveResult.State"/> is set.</summary>
    Saved,

    /// <summary>No form has that id — the caller returns 404. Creation is <c>POST /forms</c> only.</summary>
    FormNotFound,

    /// <summary>
    /// The client saved against a version/revision the server no longer holds (another session
    /// advanced the form) — the caller returns 409 and writes nothing.
    /// </summary>
    RevisionMismatch,
}

/// <summary>Outcome of a save attempt: a status plus, on <see cref="FormSaveStatus.Saved"/>, the new version state.</summary>
public readonly record struct FormSaveResult(FormSaveStatus Status, FormVersionState? State)
{
    public static FormSaveResult NotFound => new(FormSaveStatus.FormNotFound, null);

    public static FormSaveResult Conflict => new(FormSaveStatus.RevisionMismatch, null);

    public static FormSaveResult Saved(FormVersionState state) => new(FormSaveStatus.Saved, state);
}

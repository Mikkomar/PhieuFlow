namespace PhieuFlow.Persistence.Projections;

/// <summary>The three outcomes of <see cref="Repositories.IFormRepository.PublishAsync"/>.</summary>
public enum FormPublishStatus
{
    /// <summary>The validated row was flipped; <see cref="FormPublishResult.State"/> is set.</summary>
    Published,

    /// <summary>No form has that id.</summary>
    FormNotFound,

    /// <summary>
    /// The row the caller validated is no longer the one the server holds — another session's
    /// save landed between validate and flip. Nothing was published.
    /// </summary>
    RevisionMismatch,
}

/// <summary>Outcome of a publish attempt: a status plus, on <see cref="FormPublishStatus.Published"/>, the new state.</summary>
public readonly record struct FormPublishResult(FormPublishStatus Status, FormVersionState? State)
{
    public static FormPublishResult NotFound => new(FormPublishStatus.FormNotFound, null);

    public static FormPublishResult Conflict => new(FormPublishStatus.RevisionMismatch, null);

    public static FormPublishResult Published(FormVersionState state) => new(FormPublishStatus.Published, state);
}

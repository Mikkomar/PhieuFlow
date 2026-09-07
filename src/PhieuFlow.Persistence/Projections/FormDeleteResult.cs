namespace PhieuFlow.Persistence.Projections;

/// <summary>The three outcomes of <see cref="Repositories.IFormRepository.DeleteAsync"/>.</summary>
public enum FormDeleteStatus
{
    /// <summary>The form was removed; its whole version tree cascades.</summary>
    Deleted,

    /// <summary>No form has that id — the caller returns 404.</summary>
    FormNotFound,

    /// <summary>
    /// The form has at least one <see cref="Core.Entities.FormSubmission"/> — a historical
    /// record whose FKs are <c>Restrict</c>. The caller returns 409 and nothing is written.
    /// </summary>
    HasSubmissions,
}

/// <summary>Outcome of a delete attempt: just a status — a successful delete carries no state.</summary>
public readonly record struct FormDeleteResult(FormDeleteStatus Status)
{
    public static FormDeleteResult NotFound => new(FormDeleteStatus.FormNotFound);

    public static FormDeleteResult Blocked => new(FormDeleteStatus.HasSubmissions);

    public static FormDeleteResult Deleted => new(FormDeleteStatus.Deleted);
}

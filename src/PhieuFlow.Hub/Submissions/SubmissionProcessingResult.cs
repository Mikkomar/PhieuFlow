namespace PhieuFlow.Hub.Submissions;

/// <summary>
/// Outcome of <see cref="SubmissionMessageHandler.HandleAsync"/>. The consumer turns this
/// into an ack (<see cref="Persisted"/> / <see cref="DuplicateIgnored"/>) or a
/// reject-to-dead-letter (<see cref="Poison"/>). A transient fault is signalled by a thrown
/// exception, not a value here.
/// </summary>
public enum SubmissionProcessingResult
{
    /// <summary>A new submission and its inbox row were written.</summary>
    Persisted,

    /// <summary>The message id was already in the inbox; nothing written, ack it.</summary>
    DuplicateIgnored,

    /// <summary>The message cannot ever succeed (unknown form/version, unmappable answer).</summary>
    Poison,
}

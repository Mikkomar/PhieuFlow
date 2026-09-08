namespace PhieuFlow.Hub.Submissions;

/// <summary>
/// Outcome of <see cref="SubmissionMessageHandler.HandleAsync"/>: the consumer acks
/// (<see cref="Persisted"/>, <see cref="DuplicateIgnored"/>) or rejects to dead-letter
/// (<see cref="Poison"/>). A transient fault throws instead.
/// </summary>
public enum SubmissionProcessingResult
{
    /// <summary>The handler wrote a new submission and its inbox row.</summary>
    Persisted,

    /// <summary>The message id was already in the inbox. Nothing was written. Ack it.</summary>
    DuplicateIgnored,

    /// <summary>The message can never succeed (unknown form or version, or an unmappable answer).</summary>
    Poison,
}

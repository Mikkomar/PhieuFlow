namespace PhieuFlow.Hub.Contracts.Submissions;

/// <summary>
/// The single async boundary in the system (ADR 0001). The form-filler publishes a
/// <see cref="FormSubmissionRequest"/> here and the future Hub consumer drains it. It is
/// declared as a durable quorum queue so a submission survives a broker restart and a
/// redelivery counts against the delivery limit.
/// </summary>
public static class SubmissionQueue
{
    public const string Name = "form-submissions";
}

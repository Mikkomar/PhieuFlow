namespace PhieuFlow.Hub.Contracts;

/// <summary>
/// The outcome of a publish attempt. On failure (<see cref="Published"/> is <c>false</c>,
/// HTTP 422) <see cref="Form"/> is the submitted tree annotated with the problems that block
/// the publish. On success the state fields carry the server's published version.
/// </summary>
public class PublishResultDto
{
    public required bool Published { get; set; }

    /// <summary>The form tree, annotated with <see cref="ValidationIssueDto"/>s when the publish was blocked.</summary>
    public required FormDto Form { get; set; }

    public required int VersionNumber { get; set; }
    public int? LiveVersionNumber { get; set; }
    public required bool IsFirstPublish { get; set; }

    // Set only when Published is true.
    public int Revision { get; set; }
    public FormVersionStatusDto Status { get; set; }
    public DateTimeOffset LastModifiedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}

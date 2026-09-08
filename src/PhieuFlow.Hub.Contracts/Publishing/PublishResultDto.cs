using PhieuFlow.Hub.Contracts.Forms;
using PhieuFlow.Hub.Contracts.Validation;

namespace PhieuFlow.Hub.Contracts.Publishing;

/// <summary>
/// The outcome of a publish attempt. On failure (<see cref="Published"/> false, HTTP 422),
/// <see cref="Form"/> is the submitted tree annotated with the blocking problems. On
/// success the state fields carry the published version.
/// </summary>
public class PublishResultDto
{
    public required bool Published { get; set; }

    /// <summary>The form tree, annotated with <see cref="ValidationIssueDto"/>s when a publish is blocked.</summary>
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

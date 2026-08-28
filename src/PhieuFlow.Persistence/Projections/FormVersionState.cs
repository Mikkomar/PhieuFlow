using PhieuFlow.Core.Entities;

namespace PhieuFlow.Persistence.Projections;

public class FormVersionState
{
    public required int VersionNumber { get; set; }
    public required int Revision { get; set; }
    public required FormVersionStatus Status { get; set; }
    public required DateTimeOffset LastModifiedAt { get; set; }
}

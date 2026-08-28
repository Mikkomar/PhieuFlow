namespace PhieuFlow.Hub.Contracts;

public class FormVersionStateDto
{
    public required int VersionNumber { get; set; }
    public required int Revision { get; set; }
    public required FormVersionStatusDto Status { get; set; }
    public required DateTimeOffset LastModifiedAt { get; set; }
}

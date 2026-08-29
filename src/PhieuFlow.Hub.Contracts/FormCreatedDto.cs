namespace PhieuFlow.Hub.Contracts;

/// <summary>Response of <c>POST /forms</c>: the id of the freshly-minted blank draft.</summary>
public class FormCreatedDto
{
    public required Guid Id { get; set; }
}

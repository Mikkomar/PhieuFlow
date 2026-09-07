using System.Text.Json.Serialization;

namespace PhieuFlow.Hub.Contracts.Forms;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FormVersionStatusDto
{
    Draft,
    Published,
}

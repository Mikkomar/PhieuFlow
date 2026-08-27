using System.Text.Json.Serialization;

namespace PhieuFlow.Hub.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FormVersionStatusDto
{
    Draft,
    Published,
}

using System.Text.Json.Serialization;

namespace Hato.Modules.Breeding.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ServiceType
{
    Natural = 1,
    ArtificialInsemination = 2
}

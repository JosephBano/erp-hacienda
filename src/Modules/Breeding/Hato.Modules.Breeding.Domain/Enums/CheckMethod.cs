using System.Text.Json.Serialization;

namespace Hato.Modules.Breeding.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CheckMethod
{
    Palpation = 1,
    Ultrasound = 2,
    NonReturn = 3
}

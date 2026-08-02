using System.Text.Json.Serialization;

namespace Hato.Modules.Breeding.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BirthingDifficulty
{
    Normal = 1,
    Assisted = 2,
    Cesarean = 3,
    Dystocia = 4
}

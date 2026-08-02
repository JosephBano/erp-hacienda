using System.Text.Json.Serialization;

namespace Hato.Modules.Breeding.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PregnancyStatus
{
    Active = 1,
    Aborted = 2,
    Completed = 3
}

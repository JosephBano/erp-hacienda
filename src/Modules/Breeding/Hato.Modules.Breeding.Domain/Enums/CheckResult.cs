using System.Text.Json.Serialization;

namespace Hato.Modules.Breeding.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CheckResult
{
    Positive = 1,
    Negative = 2,
    Doubtful = 3
}

using System.Text.Json.Serialization;

namespace Hato.Modules.Tasks.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AlertSeverity
{
    Info = 1,
    Warning = 2,
    Critical = 3
}

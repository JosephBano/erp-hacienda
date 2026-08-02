using System.Text.Json.Serialization;

namespace Hato.Modules.Livestock.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Sex
{
    Female = 1,
    Male = 2,
}

using System.Text.Json.Serialization;

namespace Hato.Modules.People.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UserRole
{
    Admin,
    Registrar,
    Veterinarian
}

using System.Text.Json.Serialization;

namespace Hato.Modules.Livestock.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EventType
{
    Weighing,
    Treatment,
    Vaccination,
    Diagnosis,
    Movement,
    Disposal,
    Correction
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DisposalType
{
    Sale,
    Death,
    Culling,
    Stolen
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WithdrawalTarget
{
    Milk,
    Meat,
    Both
}

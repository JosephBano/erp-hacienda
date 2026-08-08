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
    Correction,
    /// <summary>
    /// Last individual footprint of an animal at the moment it joins a headcount
    /// engorde lot (3.5a.4 task 4 / ADR-0023). Animal-subject only.
    /// </summary>
    WeightSorted,
    /// <summary>
    /// Group footprint of the day N heads of a weaned cohort were mixed into a
    /// headcount engorde lot (3.5a.4 task 4 / ADR-0023). Group-subject only.
    /// </summary>
    GroupWeightSorting
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

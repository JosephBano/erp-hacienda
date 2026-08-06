using System.Text.Json.Serialization;

namespace Hato.Modules.Livestock.Domain;

/// <summary>
/// Whether a group's members are known individually or only by count (ADR-0015).
/// <see cref="Individual"/> is the default: every member is a real, identifiable
/// <see cref="Animal"/>. <see cref="Headcount"/> declares that the group knows how many
/// members it has, not which ones — the state a mixed, unmarked engorde lot is actually
/// in. It is a capability of the group, never of a species (Art. 8): the same farm has
/// individually tracked sows and headcount fattening lots at once.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TrackingMode
{
    Individual,
    Headcount
}

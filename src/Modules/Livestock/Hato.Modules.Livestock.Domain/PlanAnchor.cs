using System.Text.Json.Serialization;

namespace Hato.Modules.Livestock.Domain;

/// <summary>
/// The four closed anchors a <see cref="HealthPlanItem"/> can hang off
/// (ADR-0016 sec.1). This is the only enum in the cronograma feature: while
/// <c>event_type</c> is data (the catalogue of things the operator wants to
/// schedule keeps growing), the set of anchors is closed — adding a fifth
/// ("a los N días del primer celo") is a code change in the date resolver,
/// not a row insert. The ADR accepts that trade-off explicitly
/// (ADR-0016 sec."Negativas").
///
/// The string values are lower-case tokens the database stores verbatim and
/// the field-app reads back as-is; keeping them in sync with the constants
/// below is the contract.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PlanAnchor
{
    /// <summary>Anchor on the animal's date of birth.</summary>
    Birth,

    /// <summary>Anchor on the most recent day the animal joined the assigned group.</summary>
    GroupStart,

    /// <summary>Anchor on the last Birthing event of the animal.</summary>
    Birthing,

    /// <summary>Anchor on the WeanedAt of the nursing cohort the animal belongs to.</summary>
    Weaning,
}

public static class PlanAnchorNames
{
    public const string Birth = "Birth";
    public const string GroupStart = "GroupStart";
    public const string Birthing = "Birthing";
    public const string Weaning = "Weaning";

    public static bool IsKnown(string value) => value is Birth or GroupStart or Birthing or Weaning;
}

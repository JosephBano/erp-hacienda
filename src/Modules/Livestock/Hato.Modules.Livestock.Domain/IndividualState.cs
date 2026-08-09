namespace Hato.Modules.Livestock.Domain;

/// <summary>
/// The answer to "is this animal still alive?", aware of the group it currently belongs
/// to (ADR-0015 sec.6 — the term this glossary entry and <c>GLOSSARY.md</c> refer to as
/// <c>IndeterminateIndividualState</c>). An animal in a <see cref="TrackingMode.Headcount"/>
/// group has no individual answer, and the system says so instead of inventing one:
/// <see cref="Indeterminate"/> is not "unknown", it is "the group knows how many left, not
/// which ones".
/// </summary>
public enum IndividualState
{
    Alive,
    Indeterminate,
    Disposed
}

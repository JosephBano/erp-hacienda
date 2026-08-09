namespace Hato.Modules.Livestock.Application.AnimalGroups;

/// <summary>
/// Single source of truth for the "how many heads are alive in this group" formula
/// (ADR-0015 sec.4, ADR-0025 sec.4). The number is always derived; never stored as an
/// editable counter. Used by <see cref="GetAnimalGroupsHandler"/> for the list query
/// and by <see cref="GetAnimalGroupSummaryHandler"/> for the detail summary — keeping
/// the formula in one place so the two surfaces cannot diverge.
/// </summary>
public static class LiveHeadCountCalculator
{
    public static int Compute(int activeMemberships, int disposed)
    {
        // A lot whose head count is exhausted has zero live heads, regardless of
        // which exact sequence of disposals got us there. Clamp at zero.
        return Math.Max(0, activeMemberships - disposed);
    }
}

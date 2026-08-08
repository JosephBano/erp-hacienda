using Hato.SharedKernel;

namespace Hato.Modules.Livestock.Domain;

/// <summary>
/// The link between a <see cref="HealthPlan"/> and the subject it covers
/// (ADR-0016 sec."Decisión" 3). A plan can be assigned to a single animal
/// (the three mothers who get a different schedule) or to a whole group
/// (the feedlot receives its vaccination plan by lot). The XOR constraint
/// is enforced both at the domain level and as a DB CHECK constraint — a
/// row that points at both, or at neither, is structural nonsense.
///
/// <see cref="AssignedAt"/> is the moment the link was made. The plan does
/// not know the start date of the anchor (birth, group start, last
/// birthing); the resolver uses the subject's own data to compute the
/// theoretical date the moment the schedule is asked for.
/// </summary>
public class HealthPlanAssignment : AuditableEntity
{
    public Guid HealthPlanId { get; private set; }
    public Guid? AnimalId { get; private set; }
    public Guid? GroupId { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; }
    public bool IsActive { get; private set; }

    private HealthPlanAssignment() { }

    private HealthPlanAssignment(Guid healthPlanId, Guid? animalId, Guid? groupId, DateTimeOffset assignedAt)
    {
        HealthPlanId = healthPlanId;
        AnimalId = animalId;
        GroupId = groupId;
        AssignedAt = assignedAt;
        IsActive = true;
    }

    /// <summary>Strictly one subject per assignment (animal XOR group).</summary>
    public static HealthPlanAssignment Create(
        Guid healthPlanId,
        Guid? animalId,
        Guid? groupId,
        DateTimeOffset? assignedAt = null)
    {
        if (healthPlanId == Guid.Empty)
            throw new DomainException("La asignación debe estar vinculada a un plan sanitario.");

        var hasAnimal = animalId is not null && animalId != Guid.Empty;
        var hasGroup = groupId is not null && groupId != Guid.Empty;

        if (hasAnimal == hasGroup)
        {
            // Both true (pointed at both) or both false (pointed at neither).
            throw new DomainException(
                "Una asignación debe estar vinculada a exactamente un animal o un grupo, no a ambos ni a ninguno.");
        }

        return new HealthPlanAssignment(
            healthPlanId,
            hasAnimal ? animalId : null,
            hasGroup ? groupId : null,
            assignedAt ?? DateTimeOffset.UtcNow);
    }

    public static HealthPlanAssignment CreateForAnimal(Guid healthPlanId, Guid animalId, DateTimeOffset? assignedAt = null)
        => Create(healthPlanId, animalId, null, assignedAt);

    public static HealthPlanAssignment CreateForGroup(Guid healthPlanId, Guid groupId, DateTimeOffset? assignedAt = null)
        => Create(healthPlanId, null, groupId, assignedAt);

    public void Deactivate()
    {
        if (!IsActive)
            throw new DomainException("La asignación del plan ya está inactiva.");

        IsActive = false;
    }
}

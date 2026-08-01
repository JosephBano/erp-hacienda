using Hato.SharedKernel;

namespace Hato.Modules.Livestock.Domain;

/// <summary>
/// A management/costing group of animals (e.g. "Milking Cows", "March 2026 Feeders").
/// Membership is tracked over time with history (Art. 4 + ARCHITECTURE.md).
/// </summary>
public class GroupMembership : AuditableEntity
{
    public Guid GroupId { get; private set; }
    public Guid AnimalId { get; private set; }
    public DateOnly JoinedAt { get; private set; }
    public DateOnly? LeftAt { get; private set; }

    public bool IsActive => LeftAt is null;

    private GroupMembership() { }

    internal GroupMembership(Guid groupId, Guid animalId, DateOnly joinedAt)
    {
        if (groupId == Guid.Empty)
            throw new DomainException("La membresía debe estar vinculada a un grupo válido.");

        if (animalId == Guid.Empty)
            throw new DomainException("La membresía debe estar vinculada a un animal válido.");

        GroupId = groupId;
        AnimalId = animalId;
        JoinedAt = joinedAt;
    }

    internal void Close(DateOnly leftAt)
    {
        if (leftAt < JoinedAt)
            throw new DomainException("La fecha de salida del grupo no puede ser anterior a la fecha de ingreso.");

        LeftAt = leftAt;
    }
}

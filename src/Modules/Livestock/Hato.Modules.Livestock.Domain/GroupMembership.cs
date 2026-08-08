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

    /// <summary>
    /// Factory for the cross-module cohort-classification flow (3.5a.4 task 4,
    /// ADR-0023). The constructor is internal because most call sites go
    /// through <c>AddMemberCommand</c> or the <c>MoveAnimalPayload</c> sync
    /// path; the classification case is the only one that needs to bypass
    /// the regular write port to stay free of a reference to
    /// <c>Hato.Modules.Breeding</c>.
    /// </summary>
    public static GroupMembership Create(Guid animalId, Guid groupId, DateOnly joinedAt)
        => new(groupId, animalId, joinedAt);

    internal void Close(DateOnly leftAt)
    {
        if (leftAt < JoinedAt)
            throw new DomainException("La fecha de salida del grupo no puede ser anterior a la fecha de ingreso.");

        LeftAt = leftAt;
    }
}

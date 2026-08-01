using Hato.SharedKernel;

namespace Hato.Modules.Livestock.Domain;

/// <summary>
/// An animal group or herd batch (Art. 4 + ARCHITECTURE.md).
/// Groups serve as handling and costing units for feed, treatments, and movements.
/// </summary>
public class AnimalGroup : AuditableEntity
{
    private readonly List<GroupMembership> _memberships = [];

    public string Name { get; private set; }
    public string? Description { get; private set; }
    public Guid? SpeciesId { get; private set; }
    public bool IsActive { get; private set; }

    public IReadOnlyCollection<GroupMembership> Memberships => _memberships.AsReadOnly();

    private AnimalGroup() { Name = null!; }

    private AnimalGroup(string name, string? description, Guid? speciesId)
    {
        Name = name;
        Description = description;
        SpeciesId = speciesId;
        IsActive = true;
    }

    public static AnimalGroup Create(string name, string? description = null, Guid? speciesId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre del grupo no puede estar vacío.");

        return new AnimalGroup(name.Trim(), description?.Trim(), speciesId);
    }

    public void Update(string name, string? description = null, Guid? speciesId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre del grupo no puede estar vacío.");

        Name = name.Trim();
        Description = description?.Trim();
        SpeciesId = speciesId;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public GroupMembership AddMember(Guid animalId, DateOnly joinedAt)
    {
        if (!IsActive)
            throw new DomainException("No se pueden agregar miembros a un grupo inactivo.");

        var activeMembership = _memberships.SingleOrDefault(m => m.AnimalId == animalId && m.IsActive);
        if (activeMembership is not null)
            throw new DomainException("El animal ya forma parte activa de este grupo.");

        var membership = new GroupMembership(Id, animalId, joinedAt);
        _memberships.Add(membership);
        return membership;
    }

    public void RemoveMember(Guid animalId, DateOnly leftAt)
    {
        var activeMembership = _memberships.SingleOrDefault(m => m.AnimalId == animalId && m.IsActive);
        if (activeMembership is null)
            throw new DomainException("El animal no es un miembro activo de este grupo.");

        activeMembership.Close(leftAt);
    }
}

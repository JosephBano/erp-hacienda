using Hato.SharedKernel;

namespace Hato.Modules.Livestock.Domain;

/// <summary>
/// A generic animal of any species (Art. 8 + ADR-0006): there is no `Cow`/`Pig` table,
/// only `Animal` with a sovereign internal UUID (Art. 3) that exists whether or not the
/// animal has a tag or an official registration.
///
/// Genealogy (mother/father, dual father as animal-or-straw) is deliberately out of
/// scope here — it belongs to Breeding, Phase 2 of the roadmap — even though the data
/// model draws those foreign keys next to identity. Adding them is a non-breaking
/// migration when that phase opens.
/// </summary>
public class Animal : AuditableEntity
{
    private readonly List<AnimalIdentifier> _identifiers = [];

    public Guid SpeciesId { get; private set; }
    public Guid? BreedId { get; private set; }
    public Guid? CategoryId { get; private set; }
    public Sex Sex { get; private set; }
    public DateOnly? BirthDate { get; private set; }

    public Guid? MotherId { get; private set; }
    public Guid? FatherAnimalId { get; private set; }
    public Guid? FatherStrawId { get; private set; }
    public Guid? BirthingId { get; private set; }

    /// <summary>
    /// The occurred-at of the last accepted edit to the mutable fields below — distinct
    /// from <see cref="AuditableEntity.UpdatedAt"/>, which is server processing time.
    /// This is what the LWW conflict resolver (ADR-0008) compares against: a phone that
    /// edited an animal at 6 AM in the field and only regained signal at noon must not
    /// lose to an edit made at 7 AM just because its push happened to arrive first.
    /// </summary>
    public DateTimeOffset? LastEditedAt { get; private set; }

    public IReadOnlyCollection<AnimalIdentifier> Identifiers => _identifiers.AsReadOnly();

    private Animal(Guid speciesId, Sex sex, DateOnly? birthDate, Guid? breedId, Guid? categoryId)
    {
        SpeciesId = speciesId;
        Sex = sex;
        BirthDate = birthDate;
        BreedId = breedId;
        CategoryId = categoryId;
    }

    /// <summary>
    /// <paramref name="id"/> lets the caller supply the identity instead of generating a
    /// fresh one (Art. 3: the UUID is sovereign and can be minted on a phone with no
    /// signal). Omitting it keeps the ordinary server-side behaviour.
    /// </summary>
    public static Animal Register(
        Guid speciesId,
        Sex sex,
        DateOnly? birthDate = null,
        Guid? breedId = null,
        Guid? categoryId = null,
        Guid? id = null)
    {
        if (speciesId == Guid.Empty)
            throw new DomainException("Un animal debe pertenecer a una especie.");

        var animal = new Animal(speciesId, sex, birthDate, breedId, categoryId);

        if (id is { } requested && requested != Guid.Empty)
        {
            animal.Id = requested;
        }

        return animal;
    }

    /// <summary>
    /// Attaches a new external identifier. If one of the same type is already active,
    /// it is closed as of <paramref name="validFrom"/> first — this is how a replaced
    /// tag or a late SIFAE registration is modeled, without ever editing history.
    /// </summary>
    public AnimalIdentifier AssignIdentifier(IdentifierType type, string value, DateOnly validFrom)
    {
        var current = _identifiers.SingleOrDefault(i => i.Type == type && i.IsActive);
        current?.Close(validFrom);

        var identifier = new AnimalIdentifier(Id, type, value, validFrom);
        _identifiers.Add(identifier);
        return identifier;
    }

    public void SetGenealogy(Guid? motherId, Guid? fatherAnimalId, Guid? fatherStrawId, Guid? birthingId = null)
    {
        if (motherId.HasValue && motherId.Value == Id)
            throw new DomainException("Un animal no puede ser su propia madre.");
        if (fatherAnimalId.HasValue && fatherAnimalId.Value == Id)
            throw new DomainException("Un animal no puede ser su propio padre.");
        if (fatherAnimalId.HasValue && fatherStrawId.HasValue)
            throw new DomainException("El padre no puede ser un animal y una pajuela simultáneamente.");

        MotherId = motherId;
        FatherAnimalId = fatherAnimalId;
        FatherStrawId = fatherStrawId;
        BirthingId = birthingId;
    }

    /// <summary>
    /// Undoes a mis-registration — a double-tap in the field, a typo in the species. This
    /// is a tombstone, not a physical removal (Art. 1): the row stays, <see cref="AuditableEntity.DeletedAt"/>
    /// is set, and every field the animal ever held is left untouched. Whether it is safe
    /// to delete an animal with recorded history is an Application-layer decision — the
    /// aggregate has no visibility into events recorded against it in another module.
    /// </summary>
    public void Delete()
    {
        if (IsDeleted)
            throw new DomainException("El animal ya fue eliminado.");

        DeletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Replaces the mutable fields (breed, category, birth date guessed or learned after
    /// registration) — the "Datos de Animales" ADR-0008 names as the editable-entity
    /// example for LWW. <paramref name="editedAt"/> is the moment the edit actually
    /// happened, declared by the caller; conflict resolution across two concurrent edits
    /// lives in Application, which is where both competing writes are visible at once.
    /// </summary>
    public void Update(Guid? breedId, Guid? categoryId, DateOnly? birthDate, DateTimeOffset editedAt)
    {
        if (IsDeleted)
            throw new DomainException("No se puede editar un animal eliminado.");

        BreedId = breedId;
        CategoryId = categoryId;
        BirthDate = birthDate;
        LastEditedAt = editedAt;
    }
}

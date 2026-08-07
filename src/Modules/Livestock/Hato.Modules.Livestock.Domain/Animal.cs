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

    /// <summary>
    /// Weight at birth in kilograms (PLAN-FASE-3-5-PORCINO.md sec.3.5a.4, task 3).
    /// Captured once, at registration — the weighing of a newborn in the field is
    /// short and loud, and a late edit is a different event (a Weighing event against
    /// the calf, not a correction here). Optional because not every birthing is
    /// weighed: a stillborn is not, and a herd whose calves are not weighed is not
    /// wrong, only data-poorer than it could be.
    /// </summary>
    public decimal? BirthWeightKg { get; private set; }

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

    /// <summary>
    /// Set when this animal's membership was closed by a <see cref="TrackingMode.Headcount"/>
    /// group's cascade closure (ADR-0015 sec.7) — the lot it was last part of reached zero
    /// head. Distinct from <see cref="AuditableEntity.DeletedAt"/>, which tombstones a
    /// mis-registration: this animal really existed and really left, just "as part of this
    /// lot; its individual fate beyond that is not known" — information degraded on
    /// purpose, not a data-entry undo.
    /// </summary>
    public DateTimeOffset? DisposedAt { get; private set; }

    public IReadOnlyCollection<AnimalIdentifier> Identifiers => _identifiers.AsReadOnly();

    private Animal(Guid speciesId, Sex sex, DateOnly? birthDate, Guid? breedId, Guid? categoryId, decimal? birthWeightKg)
    {
        SpeciesId = speciesId;
        Sex = sex;
        BirthDate = birthDate;
        BreedId = breedId;
        CategoryId = categoryId;
        BirthWeightKg = birthWeightKg;
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
        decimal? birthWeightKg = null,
        Guid? id = null)
    {
        if (speciesId == Guid.Empty)
            throw new DomainException("Un animal debe pertenecer a una especie.");
        if (birthWeightKg is <= 0)
            throw new DomainException("El peso al nacer debe ser positivo.");

        var animal = new Animal(speciesId, sex, birthDate, breedId, categoryId, birthWeightKg);

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
    /// Closes this animal as part of a lot's cascade disposal (ADR-0015 sec.7) — called
    /// once, by the group whose <c>Headcount</c> just reached zero, for every membership it
    /// closed in the same operation. Carries a different provenance flag in the field-app
    /// ("baja con alcance de lote") from <see cref="Dispose"/> ("baja identificada: se sabe
    /// cuál animal salió"), but the underlying <see cref="DisposedAt"/> state is the same.
    /// </summary>
    public void CloseViaLotDisposal(DateTimeOffset disposedAt)
    {
        MarkDisposed(disposedAt, provenance: "lot");
    }

    /// <summary>
    /// Records an identified, individual disposal (sale, death with cause, theft) — the
    /// animal whose id is on the <see cref="AnimalEvent"/> is the same animal whose
    /// <see cref="DisposedAt"/> is set here. Until 2026-08-07 the handler that wrote the
    /// event never touched this field, so any animal whose exit was registered individually
    /// (the common case — piglets that die before the litter mixes, identified sales)
    /// continued to read as <see cref="IndividualState.Alive"/> or
    /// <see cref="IndividualState.Indeterminate"/> in every aggregate that asked, including
    /// the pre-weaning mortality roll-up that 3.5a.3 was built to make honest.
    /// </summary>
    public void Dispose(DateTimeOffset disposedAt)
    {
        MarkDisposed(disposedAt, provenance: "individual");
    }

    /// <summary>
    /// Idempotency boundary for both <see cref="CloseViaLotDisposal"/> and
    /// <see cref="Dispose"/>: an animal can only be disposed once, regardless of whether
    /// the caller reached this method through an identified sale or a cascade. The
    /// provenance argument is the message of the loud-not-silent failure so a misordered
    /// caller learns which path collided with the other.
    /// </summary>
    private void MarkDisposed(DateTimeOffset disposedAt, string provenance)
    {
        if (DisposedAt is not null)
        {
            throw new DomainException(
                $"El animal ya fue dado de baja ({provenance}); una baja individual no puede seguir a otra ni a un cierre de lote.");
        }

        DisposedAt = disposedAt;
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

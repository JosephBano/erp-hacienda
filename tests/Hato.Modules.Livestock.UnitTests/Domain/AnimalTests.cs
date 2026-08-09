using Hato.Modules.Livestock.Domain;
using Hato.SharedKernel;

namespace Hato.Modules.Livestock.UnitTests.Domain;

public class AnimalTests
{
    private static readonly Guid SpeciesId = Guid.NewGuid();

    [Fact]
    public void Register_WithValidSpecies_GetsSovereignIdAndNoIdentifiers()
    {
        var animal = Animal.Register(SpeciesId, Sex.Female);

        Assert.NotEqual(Guid.Empty, animal.Id);
        Assert.Empty(animal.Identifiers);
    }

    [Fact]
    public void Register_WithoutSpecies_Throws()
    {
        Assert.Throws<DomainException>(() => Animal.Register(Guid.Empty, Sex.Female));
    }

    [Fact]
    public void Register_WithPositiveBirthWeight_StoresIt()
    {
        var animal = Animal.Register(SpeciesId, Sex.Female, birthWeightKg: 1.4m);

        Assert.Equal(1.4m, animal.BirthWeightKg);
    }

    [Fact]
    public void Register_WithoutBirthWeight_LeavesItNull()
    {
        var animal = Animal.Register(SpeciesId, Sex.Female);

        Assert.Null(animal.BirthWeightKg);
    }

    [Fact]
    public void Register_WithZeroOrNegativeBirthWeight_Throws()
    {
        Assert.Throws<DomainException>(() =>
            Animal.Register(SpeciesId, Sex.Female, birthWeightKg: 0m));
        Assert.Throws<DomainException>(() =>
            Animal.Register(SpeciesId, Sex.Female, birthWeightKg: -0.1m));
    }

    [Fact]
    public void Dispose_SetsDisposedAt()
    {
        var animal = Animal.Register(SpeciesId, Sex.Female);
        var occurredAt = new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero);

        animal.Dispose(occurredAt);

        Assert.Equal(occurredAt, animal.DisposedAt);
    }

    [Fact]
    public void Dispose_Twice_Throws()
    {
        var animal = Animal.Register(SpeciesId, Sex.Female);
        animal.Dispose(DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(() => animal.Dispose(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Dispose_AfterLotCascade_Throws()
    {
        var animal = Animal.Register(SpeciesId, Sex.Female);
        animal.CloseViaLotDisposal(DateTimeOffset.UtcNow);

        // A second individual disposal must not silently overwrite the cascade's flag.
        Assert.Throws<DomainException>(() => animal.Dispose(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void CloseViaLotDisposal_AfterIndividualDispose_Throws()
    {
        var animal = Animal.Register(SpeciesId, Sex.Female);
        animal.Dispose(DateTimeOffset.UtcNow);

        // A late cascade (e.g. an offline record that syncs after the individual sale)
        // must not silently overwrite the individual disposal.
        Assert.Throws<DomainException>(() => animal.CloseViaLotDisposal(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Register_WithoutTagOrRegistration_IsStillValid()
    {
        // Domain warning in AGENTS.md: an animal can be fully valid with no tag/SIFAE.
        var animal = Animal.Register(SpeciesId, Sex.Male, birthDate: new DateOnly(2026, 1, 1));

        Assert.Empty(animal.Identifiers);
    }

    [Fact]
    public void AssignIdentifier_FirstOfItsType_IsActive()
    {
        var animal = Animal.Register(SpeciesId, Sex.Female);

        var identifier = animal.AssignIdentifier(IdentifierType.FarmTag, "A-101", new DateOnly(2026, 1, 1));

        Assert.True(identifier.IsActive);
        Assert.Single(animal.Identifiers);
    }

    [Fact]
    public void AssignIdentifier_ReplacingSameType_ClosesThePreviousOneInstead()
    {
        var animal = Animal.Register(SpeciesId, Sex.Female);
        var lost = animal.AssignIdentifier(IdentifierType.FarmTag, "A-101", new DateOnly(2026, 1, 1));

        var replacement = animal.AssignIdentifier(IdentifierType.FarmTag, "A-102", new DateOnly(2026, 3, 1));

        Assert.False(lost.IsActive);
        Assert.Equal(new DateOnly(2026, 3, 1), lost.ValidTo);
        Assert.True(replacement.IsActive);
        Assert.Equal(2, animal.Identifiers.Count);
    }

    [Fact]
    public void AssignIdentifier_DifferentTypes_CoexistActive()
    {
        var animal = Animal.Register(SpeciesId, Sex.Female);

        var farmTag = animal.AssignIdentifier(IdentifierType.FarmTag, "A-101", new DateOnly(2026, 1, 1));
        var officialTag = animal.AssignIdentifier(IdentifierType.OfficialTag, "EC-9988", new DateOnly(2026, 2, 15));

        Assert.True(farmTag.IsActive);
        Assert.True(officialTag.IsActive);
    }

    [Fact]
    public void AssignIdentifier_WithBlankValue_Throws()
    {
        var animal = Animal.Register(SpeciesId, Sex.Female);

        Assert.Throws<DomainException>(
            () => animal.AssignIdentifier(IdentifierType.FarmTag, " ", new DateOnly(2026, 1, 1)));
    }

    /// <summary>
    /// Undoes a mis-registration (a double-tap in the field, a typo in the species).
    /// Art. 1 is honored because this never removes the row — it sets DeletedAt, which
    /// is what turns it into a tombstone the sync pull propagates to every device.
    /// </summary>
    [Fact]
    public void Delete_MarksTheAnimalAsDeletedWithoutClearingItsData()
    {
        var animal = Animal.Register(SpeciesId, Sex.Female, breedId: Guid.NewGuid());

        animal.Delete();

        Assert.True(animal.IsDeleted);
        Assert.NotNull(animal.DeletedAt);
        Assert.Equal(SpeciesId, animal.SpeciesId);
    }

    [Fact]
    public void Delete_CalledTwice_Throws()
    {
        var animal = Animal.Register(SpeciesId, Sex.Female);
        animal.Delete();

        Assert.Throws<DomainException>(() => animal.Delete());
    }

    /// <summary>
    /// Corrections to mutable fields (a breed guessed wrong at registration, a birth date
    /// learned later). <see cref="Animal.LastEditedAt"/> is set from the caller's declared
    /// moment, not the server clock — it is what the LWW conflict resolver in
    /// Application compares against, and using the server's processing time instead would
    /// make the outcome depend on network luck rather than which edit actually happened
    /// later in the field.
    /// </summary>
    [Fact]
    public void Update_SetsEditableFieldsAndTheDeclaredEditMoment()
    {
        var animal = Animal.Register(SpeciesId, Sex.Female);
        var breedId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var birthDate = new DateOnly(2026, 1, 1);
        var editedAt = new DateTimeOffset(2026, 5, 1, 6, 0, 0, TimeSpan.Zero);

        animal.Update(breedId, categoryId, birthDate, editedAt);

        Assert.Equal(breedId, animal.BreedId);
        Assert.Equal(categoryId, animal.CategoryId);
        Assert.Equal(birthDate, animal.BirthDate);
        Assert.Equal(editedAt, animal.LastEditedAt);
    }

    [Fact]
    public void Update_CanClearAPreviouslySetField()
    {
        var animal = Animal.Register(SpeciesId, Sex.Female, breedId: Guid.NewGuid());

        animal.Update(breedId: null, categoryId: null, birthDate: null, DateTimeOffset.UtcNow);

        Assert.Null(animal.BreedId);
    }

    [Fact]
    public void Update_OnADeletedAnimal_Throws()
    {
        var animal = Animal.Register(SpeciesId, Sex.Female);
        animal.Delete();

        Assert.Throws<DomainException>(
            () => animal.Update(Guid.NewGuid(), null, null, DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// ADR-0015 sec.7: the cascade closure marks an animal disposed "with lot scope" —
    /// it really left, the system just cannot say more than that. Distinct from
    /// <see cref="Animal.Delete"/>, which tombstones a mis-registration.
    /// </summary>
    [Fact]
    public void CloseViaLotDisposal_SetsDisposedAt_WithoutTouchingDeletedAt()
    {
        var animal = Animal.Register(SpeciesId, Sex.Male);
        var disposedAt = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        animal.CloseViaLotDisposal(disposedAt);

        Assert.Equal(disposedAt, animal.DisposedAt);
        Assert.False(animal.IsDeleted);
    }

    [Fact]
    public void CloseViaLotDisposal_CalledTwice_Throws()
    {
        var animal = Animal.Register(SpeciesId, Sex.Male);
        animal.CloseViaLotDisposal(DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(() => animal.CloseViaLotDisposal(DateTimeOffset.UtcNow));
    }
}

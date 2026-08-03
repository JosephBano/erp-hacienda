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
}

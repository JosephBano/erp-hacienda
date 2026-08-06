using Hato.Modules.People.Domain;
using Xunit;

namespace Hato.Modules.People.UnitTests;

public class SyncConflictTests
{
    [Fact]
    public void Create_WithValidData_RecordsWhatWasThereAndWhatWasAttempted()
    {
        var animalId = Guid.NewGuid();
        var operationId = Guid.NewGuid();

        var conflict = SyncConflict.Create(
            "Animal", animalId, "BreedId",
            serverValue: "breed-A", attemptedValue: "breed-B",
            resolution: SyncConflictResolution.ServerWon,
            clientOperationId: operationId, deviceId: "device-2");

        Assert.Equal("Animal", conflict.EntityType);
        Assert.Equal(animalId, conflict.EntityId);
        Assert.Equal("BreedId", conflict.FieldName);
        Assert.Equal("breed-A", conflict.ServerValue);
        Assert.Equal("breed-B", conflict.AttemptedValue);
        Assert.Equal(nameof(SyncConflictResolution.ServerWon), conflict.Resolution);
        Assert.Equal(operationId, conflict.ClientOperationId);
    }

    [Fact]
    public void Create_WithoutEntityType_Throws()
    {
        Assert.Throws<ArgumentException>(() => SyncConflict.Create(
            " ", Guid.NewGuid(), "BreedId", null, null, SyncConflictResolution.ClientWon));
    }

    [Fact]
    public void Create_WithEmptyEntityId_Throws()
    {
        Assert.Throws<ArgumentException>(() => SyncConflict.Create(
            "Animal", Guid.Empty, "BreedId", null, null, SyncConflictResolution.ClientWon));
    }
}

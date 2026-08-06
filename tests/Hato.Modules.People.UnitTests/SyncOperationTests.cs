using Hato.Modules.People.Domain;
using Hato.SharedKernel;
using Xunit;

namespace Hato.Modules.People.UnitTests;

public class SyncOperationTests
{
    /// <summary>
    /// A freshly claimed operation has not run yet. Starting it as <c>Accepted</c> would
    /// mean a server that crashes mid-push leaves behind rows claiming success for work
    /// that never happened.
    /// </summary>
    [Fact]
    public void SyncOperation_Creation_StartsAsProcessing()
    {
        var clientOpId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var op = new SyncOperation(
            clientOpId,
            userId,
            "device-1",
            "createAnimal",
            "{}",
            now,
            now);

        Assert.Equal(clientOpId, op.ClientOperationId);
        Assert.Equal(userId, op.UserId);
        Assert.Equal("device-1", op.DeviceId);
        Assert.Equal(SyncOperationStatus.Processing, op.Status);
    }

    [Fact]
    public void SyncOperation_EmptyClientOpId_ThrowsException()
    {
        Assert.Throws<DomainException>(() => new SyncOperation(
            Guid.Empty,
            Guid.NewGuid(),
            "device-1",
            "createAnimal",
            "{}",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void SyncOperation_MarkDuplicate_UpdatesStatus()
    {
        var op = new SyncOperation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "device-1",
            "createAnimal",
            "{}",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        op.MarkDuplicate("ref-123");

        Assert.Equal(SyncOperationStatus.Duplicate, op.Status);
        Assert.Equal("ref-123", op.ResultRef);
    }

    [Fact]
    public void SyncOperation_MarkRejected_StoresErrorDetails()
    {
        var op = new SyncOperation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "device-1",
            "createAnimal",
            "{}",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        op.MarkRejected("Invalid animal species");

        Assert.Equal(SyncOperationStatus.Rejected, op.Status);
        Assert.Equal("Invalid animal species", op.ErrorDetails);
    }
}

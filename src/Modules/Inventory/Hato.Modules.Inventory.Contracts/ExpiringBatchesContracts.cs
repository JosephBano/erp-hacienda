namespace Hato.Modules.Inventory.Contracts;

public record ExpiringBatchDto(
    Guid BatchId,
    Guid InventoryItemId,
    string ItemName,
    string BatchNumber,
    DateOnly ExpirationDate,
    decimal Quantity);

/// <summary>Public read port used by Tasks/Alerts (Art. 6) instead of raw SQL against inventory tables.</summary>
public interface IExpiringBatchesReader
{
    Task<IReadOnlyList<ExpiringBatchDto>> GetExpiringWithinAsync(DateOnly thresholdDate, CancellationToken cancellationToken);
}

using Hato.SharedKernel;

namespace Hato.Modules.Breeding.Domain;

public class SemenStraw : AuditableEntity
{
    public string Code { get; private set; } = string.Empty;
    public string BullName { get; private set; } = string.Empty;
    public string? BullCode { get; private set; }
    public Guid BreedId { get; private set; }
    public string? SupplierName { get; private set; }
    public int InitialQuantity { get; private set; }
    public int CurrentQuantity { get; private set; }
    public string? Notes { get; private set; }

    private SemenStraw() { } // EF Core

    public static SemenStraw Create(
        string code,
        string bullName,
        Guid breedId,
        int quantity,
        string? bullCode = null,
        string? supplierName = null,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("Semen straw code is required.");
        if (string.IsNullOrWhiteSpace(bullName))
            throw new DomainException("Bull name is required.");
        if (quantity <= 0)
            throw new DomainException("Initial quantity must be greater than zero.");

        var straw = new SemenStraw
        {
            Id = Guid.NewGuid(),
            Code = code.Trim(),
            BullName = bullName.Trim(),
            BullCode = bullCode?.Trim(),
            BreedId = breedId,
            SupplierName = supplierName?.Trim(),
            InitialQuantity = quantity,
            CurrentQuantity = quantity,
            Notes = notes?.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        return straw;
    }

    public void UseStraw()
    {
        if (CurrentQuantity <= 0)
            throw new DomainException($"No remaining straws for bull '{BullName}' (Code: {Code}).");

        CurrentQuantity--;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Restock(int count)
    {
        if (count <= 0)
            throw new DomainException("Restock quantity must be positive.");

        CurrentQuantity += count;
        InitialQuantity += count;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

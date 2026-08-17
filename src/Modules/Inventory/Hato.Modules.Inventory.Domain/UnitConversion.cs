using Hato.SharedKernel;

namespace Hato.Modules.Inventory.Domain;

/// <summary>
/// Conversion between two units of the same <see cref="InventoryItem"/>. The
/// "bug del saco" — buying in 40-kg sacks and consuming in kg without the numbers
/// talking to each other — is closed by storing the conversion factor at the item
/// level (Art. 8: factor is data, not code). A row exists per (item, from_unit, to_unit)
/// pair; the canonical direction is <c>from_unit -> to_unit</c> with factor = how many
/// <c>to_unit</c> a single <c>from_unit</c> contains. <c>kg -> saco40kg</c> with
/// factor 0.025 is the inverse and lives on its own row.
/// (docs/spec/plan-0002-fase-3-5/spec-3.5a.md sec.3.5a.5 task 1.)
/// </summary>
public class UnitConversion : AuditableEntity
{
    public Guid InventoryItemId { get; private set; }
    public string FromUnit { get; private set; }
    public string ToUnit { get; private set; }
    public decimal Factor { get; private set; }

    private UnitConversion()
    {
        FromUnit = null!;
        ToUnit = null!;
    }

    private UnitConversion(Guid inventoryItemId, string fromUnit, string toUnit, decimal factor)
    {
        InventoryItemId = inventoryItemId;
        FromUnit = fromUnit;
        ToUnit = toUnit;
        Factor = factor;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public static UnitConversion Register(Guid inventoryItemId, string fromUnit, string toUnit, decimal factor)
    {
        if (inventoryItemId == Guid.Empty)
            throw new DomainException("La conversión debe estar vinculada a un ítem válido.");
        if (string.IsNullOrWhiteSpace(fromUnit))
            throw new DomainException("La unidad de origen no puede estar vacía.");
        if (string.IsNullOrWhiteSpace(toUnit))
            throw new DomainException("La unidad de destino no puede estar vacía.");
        if (factor <= 0)
            throw new DomainException("El factor de conversión debe ser positivo.");

        return new UnitConversion(
            inventoryItemId,
            fromUnit.Trim(),
            toUnit.Trim(),
            factor);
    }
}
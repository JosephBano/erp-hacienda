using System.Text.Json.Serialization;
using Hato.SharedKernel;

namespace Hato.Modules.Production.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MilkingShift
{
    Morning,
    Afternoon,
    Night
}

/// <summary>
/// Daily milking session (Art. 4 + GLOSSARY.md).
/// Tracks total milk yield per session for a group or whole herd.
/// </summary>
public class MilkingSession : AuditableEntity
{
    private readonly List<MilkYield> _yields = [];

    public DateOnly Date { get; private set; }
    public MilkingShift Shift { get; private set; }
    public Guid? GroupId { get; private set; }
    public decimal TotalLiters { get; private set; }
    public Guid? RecordedById { get; private set; }
    public string RecordedByLabel { get; private set; }
    public string RecordedBy => RecordedByLabel;
    public string? Notes { get; private set; }

    public IReadOnlyCollection<MilkYield> Yields => _yields.AsReadOnly();

    private MilkingSession()
    {
        RecordedByLabel = null!;
    }

    private MilkingSession(
        DateOnly date,
        MilkingShift shift,
        Guid? groupId,
        decimal totalLiters,
        string recordedByLabel,
        Guid? recordedById,
        string? notes)
    {
        Date = date;
        Shift = shift;
        GroupId = groupId;
        TotalLiters = totalLiters;
        RecordedByLabel = recordedByLabel;
        RecordedById = recordedById;
        Notes = notes;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public static MilkingSession Create(
        DateOnly date,
        MilkingShift shift,
        string recordedBy,
        decimal totalLiters = 0,
        Guid? groupId = null,
        string? notes = null,
        Guid? recordedById = null)
    {
        if (string.IsNullOrWhiteSpace(recordedBy))
            throw new DomainException("El registrador del ordeño es requerido.");

        if (totalLiters < 0)
            throw new DomainException("Los litros totales no pueden ser negativos.");

        return new MilkingSession(date, shift, groupId, totalLiters, recordedBy.Trim(), recordedById, notes?.Trim());
    }

    public MilkYield RecordAnimalYield(Guid animalId, decimal liters)
    {
        if (liters <= 0)
            throw new DomainException("La cantidad de litros por animal debe ser mayor a cero.");

        var yield = new MilkYield(Id, animalId, liters);
        _yields.Add(yield);
        TotalLiters = _yields.Sum(y => y.Liters);
        return yield;
    }
}

public class MilkYield : AuditableEntity
{
    public Guid MilkingSessionId { get; private set; }
    public Guid AnimalId { get; private set; }
    public decimal Liters { get; private set; }

    private MilkYield() { }

    internal MilkYield(Guid milkingSessionId, Guid animalId, decimal liters)
    {
        if (milkingSessionId == Guid.Empty)
            throw new DomainException("El registro de ordeño debe estar vinculado a una sesión.");

        if (animalId == Guid.Empty)
            throw new DomainException("El registro de ordeño debe estar vinculado a un animal.");

        if (liters <= 0)
            throw new DomainException("Los litros registrados deben ser mayor a cero.");

        MilkingSessionId = milkingSessionId;
        AnimalId = animalId;
        Liters = liters;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}

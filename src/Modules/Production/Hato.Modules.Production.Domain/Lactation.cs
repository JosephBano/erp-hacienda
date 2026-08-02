using Hato.SharedKernel;

namespace Hato.Modules.Production.Domain;

/// <summary>
/// A productive lactation period between calving and drying off (GLOSSARY.md).
/// </summary>
public class Lactation : AuditableEntity
{
    public Guid AnimalId { get; private set; }
    public int LactationNumber { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }

    public bool IsActive => EndDate is null;

    private Lactation() { }

    private Lactation(Guid animalId, int lactationNumber, DateOnly startDate)
    {
        AnimalId = animalId;
        LactationNumber = lactationNumber;
        StartDate = startDate;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public static Lactation Start(Guid animalId, int lactationNumber, DateOnly startDate)
    {
        if (animalId == Guid.Empty)
            throw new DomainException("La lactancia debe pertenecer a un animal válido.");

        if (lactationNumber <= 0)
            throw new DomainException("El número de lactancia debe ser mayor a cero.");

        return new Lactation(animalId, lactationNumber, startDate);
    }

    public void Close(DateOnly endDate)
    {
        if (endDate < StartDate)
            throw new DomainException("La fecha de secado/fin de lactancia no puede ser anterior al inicio.");

        EndDate = endDate;
    }
}

using Hato.SharedKernel;

namespace Hato.Modules.Livestock.Domain;

/// <summary>
/// An external identifier attached to an animal, with a temporal validity window.
/// Never a primary key (Art. 3) — always adjunct to the animal's sovereign internal UUID.
/// Only <see cref="Animal"/> can create or close one, so the "one active identifier per
/// type" invariant lives in a single place.
/// </summary>
public class AnimalIdentifier : AuditableEntity
{
    public Guid AnimalId { get; private set; }
    public IdentifierType Type { get; private set; }
    public string Value { get; private set; }
    public DateOnly ValidFrom { get; private set; }
    public DateOnly? ValidTo { get; private set; }

    public bool IsActive => ValidTo is null;

    internal AnimalIdentifier(Guid animalId, IdentifierType type, string value, DateOnly validFrom)
    {
        if (animalId == Guid.Empty)
            throw new DomainException("Una identificación debe pertenecer a un animal.");

        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("El valor de la identificación no puede estar vacío.");

        AnimalId = animalId;
        Type = type;
        Value = value.Trim();
        ValidFrom = validFrom;
    }

    internal void Close(DateOnly validTo)
    {
        if (validTo < ValidFrom)
            throw new DomainException("La fecha de baja de una identificación no puede ser anterior a su asignación.");

        ValidTo = validTo;
    }
}

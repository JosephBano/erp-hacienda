using Hato.SharedKernel;

namespace Hato.Modules.Livestock.Domain;

/// <summary>A breed within a species (Holstein, Brahman, Landrace…). Configuration data.</summary>
public class Breed : AuditableEntity
{
    public Guid SpeciesId { get; private set; }
    public string Name { get; private set; }

    private Breed(Guid speciesId, string name)
    {
        SpeciesId = speciesId;
        Name = name;
    }

    public static Breed Create(Guid speciesId, string name)
    {
        if (speciesId == Guid.Empty)
            throw new DomainException("Una raza debe pertenecer a una especie.");

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre de la raza no puede estar vacío.");

        return new Breed(speciesId, name.Trim());
    }
}

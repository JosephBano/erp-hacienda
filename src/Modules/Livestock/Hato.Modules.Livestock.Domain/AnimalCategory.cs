using Hato.SharedKernel;

namespace Hato.Modules.Livestock.Domain;

/// <summary>
/// The current life stage/use of an animal within a species (ternera, vacona, vaca en
/// producción, lechón…). Configuration data; the history of category changes lives in
/// AnimalEvents (Phase 1 event core), not here.
/// </summary>
public class AnimalCategory : AuditableEntity
{
    public Guid SpeciesId { get; private set; }
    public string Name { get; private set; }

    private AnimalCategory(Guid speciesId, string name)
    {
        SpeciesId = speciesId;
        Name = name;
    }

    public static AnimalCategory Create(Guid speciesId, string name)
    {
        if (speciesId == Guid.Empty)
            throw new DomainException("Una categoría debe pertenecer a una especie.");

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre de la categoría no puede estar vacío.");

        return new AnimalCategory(speciesId, name.Trim());
    }
}

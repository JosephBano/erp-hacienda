namespace Hato.Modules.Livestock.Contracts;

public record AnimalMilkingEligibilityDto(
    Guid AnimalId,
    bool Exists,
    bool IsFemale,
    bool IsSpeciesMilkable,
    DateTimeOffset? DisposedAt,
    string? SpeciesName);

/// <summary>
/// Public read port for Production to verify an animal's milking fitness
/// (female sex, milkable species, not disposed, exists) without Production
/// coupling to Livestock's DbContext or domain entities (Art. 6, D2).
/// </summary>
public interface IMilkingEligibilityReader
{
    Task<AnimalMilkingEligibilityDto?> GetEligibilityAsync(Guid animalId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, AnimalMilkingEligibilityDto>> GetEligibilityBatchAsync(
        IReadOnlyCollection<Guid> animalIds, CancellationToken cancellationToken);
}

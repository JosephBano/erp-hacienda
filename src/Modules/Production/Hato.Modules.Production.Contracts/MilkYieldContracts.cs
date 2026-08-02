namespace Hato.Modules.Production.Contracts;

public record AnimalMilkYieldDto(Guid Id, DateOnly Date, string Shift, decimal Liters);

/// <summary>Public read port used by Livestock's animal detail screen (Art. 6).</summary>
public interface IMilkYieldsReader
{
    Task<IReadOnlyList<AnimalMilkYieldDto>> GetByAnimalIdAsync(Guid animalId, CancellationToken cancellationToken);
}

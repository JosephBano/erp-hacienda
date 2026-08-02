using Hato.Modules.Breeding.Application.Abstractions;
using Hato.Modules.Breeding.Contracts;
using Hato.Modules.Livestock.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Breeding.Application.Pedigree;

public record GetPedigreeQuery(Guid AnimalId, int MaxGenerations = 4) : IRequest<PedigreeDto?>;

public class GetPedigreeQueryHandler(IBreedingDbContext breedingDb, IAnimalGenealogyReader genealogyReader)
    : IRequestHandler<GetPedigreeQuery, PedigreeDto?>
{
    public async Task<PedigreeDto?> Handle(GetPedigreeQuery request, CancellationToken cancellationToken)
    {
        // Ancestry itself is Livestock data, read through its public contract (Art. 6)
        // instead of Breeding running SQL against livestock.animals directly.
        var ancestry = await genealogyReader.GetAncestryAsync(request.AnimalId, request.MaxGenerations, cancellationToken);
        if (ancestry.Count == 0)
            return null;

        var strawIds = ancestry
            .Where(a => a.FatherStrawId.HasValue)
            .Select(a => a.FatherStrawId!.Value)
            .Distinct()
            .ToList();

        var strawBullNames = strawIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await breedingDb.SemenStraws
                .AsNoTracking()
                .Where(s => strawIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.BullName, cancellationToken);

        var ancestors = ancestry
            .Select(a => new AncestorDto(
                a.AnimalId,
                a.FarmTag,
                a.Sex,
                a.GenerationLevel,
                a.Role,
                a.MotherId,
                a.FatherAnimalId,
                a.FatherStrawId,
                a.FatherStrawId.HasValue && strawBullNames.TryGetValue(a.FatherStrawId.Value, out var bullName) ? bullName : null))
            .ToList();

        var target = ancestors.First(a => a.GenerationLevel == 0);
        return new PedigreeDto(
            target.AnimalId,
            target.FarmTag,
            ancestors.Where(a => a.GenerationLevel > 0).ToList()
        );
    }
}

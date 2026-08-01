using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.Animals;

public record AnimalListItemDto(
    Guid Id,
    string? FarmTag,
    string? OfficialTag,
    string? Name,
    DateOnly? BirthDate,
    string Gender,
    string? SpeciesName,
    string? BreedName,
    string? CategoryName,
    string Status,
    bool IsInWithdrawal,
    DateOnly? WithdrawalUntil);

public record GetAnimalsQuery : IRequest<List<AnimalListItemDto>>;

/// <summary>Backs the admin panel's animal list/filter screen (Fase 1 exit criterion).</summary>
public class GetAnimalsHandler(ILivestockDbContext dbContext) : IRequestHandler<GetAnimalsQuery, List<AnimalListItemDto>>
{
    public async Task<List<AnimalListItemDto>> Handle(GetAnimalsQuery request, CancellationToken cancellationToken)
    {
        var animals = await dbContext.Animals
            .AsNoTracking()
            .Include(a => a.Identifiers)
            .ToListAsync(cancellationToken);

        if (animals.Count == 0)
            return [];

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var animalIds = animals.Select(a => a.Id).ToList();

        var speciesNames = await dbContext.Species.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);
        var breedNames = await dbContext.Breeds.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.Name, cancellationToken);
        var categoryNames = await dbContext.AnimalCategories.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var withdrawalUntilByAnimal = await dbContext.WithdrawalPeriods
            .AsNoTracking()
            .Where(w => animalIds.Contains(w.AnimalId) && w.StartsAt <= today && w.EndsAt >= today)
            .GroupBy(w => w.AnimalId)
            .Select(g => new { AnimalId = g.Key, Until = g.Max(w => w.EndsAt) })
            .ToDictionaryAsync(x => x.AnimalId, x => x.Until, cancellationToken);

        var disposedAnimalIds = (await dbContext.AnimalEvents
            .AsNoTracking()
            .Where(e => animalIds.Contains(e.AnimalId) && e.EventType == EventType.Disposal)
            .Select(e => e.AnimalId)
            .Distinct()
            .ToListAsync(cancellationToken))
            .ToHashSet();

        return animals.Select(a =>
        {
            var isInWithdrawal = withdrawalUntilByAnimal.TryGetValue(a.Id, out var until);

            return new AnimalListItemDto(
                a.Id,
                FirstActiveIdentifier(a, IdentifierType.FarmTag),
                FirstActiveIdentifier(a, IdentifierType.OfficialTag),
                FirstActiveIdentifier(a, IdentifierType.Name),
                a.BirthDate,
                a.Sex.ToString(),
                speciesNames.GetValueOrDefault(a.SpeciesId),
                a.BreedId.HasValue ? breedNames.GetValueOrDefault(a.BreedId.Value) : null,
                a.CategoryId.HasValue ? categoryNames.GetValueOrDefault(a.CategoryId.Value) : null,
                disposedAnimalIds.Contains(a.Id) ? "Disposed" : "Active",
                isInWithdrawal,
                isInWithdrawal ? until : null);
        }).ToList();
    }

    private static string? FirstActiveIdentifier(Animal animal, IdentifierType type) =>
        animal.Identifiers.Where(i => i.Type == type && i.IsActive).Select(i => i.Value).FirstOrDefault();
}

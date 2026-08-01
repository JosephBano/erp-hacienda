using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Domain;
using Hato.Modules.Production.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.Animals;

public record GetAnimalByIdQuery(Guid AnimalId) : IRequest<AnimalDto?>;

public record AnimalIdentifierDto(IdentifierType Type, string Value, DateOnly ValidFrom, DateOnly? ValidTo);

public record AnimalEventSummaryDto(Guid Id, string EventType, DateTimeOffset EventDate, string DetailsJson, string RecordedBy);

public record AnimalMilkYieldSummaryDto(Guid Id, DateOnly Date, string Session, decimal Liters);

public record AnimalDto(
    Guid Id,
    Guid SpeciesId,
    Guid? BreedId,
    Guid? CategoryId,
    Sex Sex,
    DateOnly? BirthDate,
    IReadOnlyCollection<AnimalIdentifierDto> Identifiers,
    string? FarmTag = null,
    string? OfficialTag = null,
    string? Name = null,
    string? SpeciesName = null,
    string? BreedName = null,
    string? CategoryName = null,
    string Status = "Active",
    bool IsInWithdrawal = false,
    DateOnly? WithdrawalUntil = null,
    IReadOnlyCollection<AnimalEventSummaryDto>? Events = null,
    IReadOnlyCollection<AnimalMilkYieldSummaryDto>? MilkYields = null,
    Guid? MotherId = null,
    Guid? FatherAnimalId = null,
    Guid? FatherStrawId = null,
    Guid? BirthingId = null);

public class GetAnimalByIdHandler(ILivestockDbContext dbContext, IMilkYieldsReader milkYieldsReader)
    : IRequestHandler<GetAnimalByIdQuery, AnimalDto?>
{
    public async Task<AnimalDto?> Handle(GetAnimalByIdQuery request, CancellationToken cancellationToken)
    {
        var animal = await dbContext.Animals
            .Include(a => a.Identifiers)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.AnimalId, cancellationToken);

        if (animal is null)
            return null;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var speciesName = await dbContext.Species.AsNoTracking()
            .Where(s => s.Id == animal.SpeciesId).Select(s => s.Name).FirstOrDefaultAsync(cancellationToken);
        var breedName = animal.BreedId.HasValue
            ? await dbContext.Breeds.AsNoTracking().Where(b => b.Id == animal.BreedId.Value).Select(b => b.Name).FirstOrDefaultAsync(cancellationToken)
            : null;
        var categoryName = animal.CategoryId.HasValue
            ? await dbContext.AnimalCategories.AsNoTracking().Where(c => c.Id == animal.CategoryId.Value).Select(c => c.Name).FirstOrDefaultAsync(cancellationToken)
            : null;

        var withdrawalUntil = await dbContext.WithdrawalPeriods.AsNoTracking()
            .Where(w => w.AnimalId == animal.Id && w.StartsAt <= today && w.EndsAt >= today)
            .Select(w => (DateOnly?)w.EndsAt)
            .OrderByDescending(d => d)
            .FirstOrDefaultAsync(cancellationToken);

        var hasDisposal = await dbContext.AnimalEvents.AsNoTracking()
            .AnyAsync(e => e.AnimalId == animal.Id && e.EventType == EventType.Disposal, cancellationToken);

        var events = await dbContext.AnimalEvents.AsNoTracking()
            .Where(e => e.AnimalId == animal.Id)
            .OrderByDescending(e => e.OccurredAt)
            .Select(e => new AnimalEventSummaryDto(e.Id, e.EventType.ToString(), e.OccurredAt, e.PayloadJson, e.RecordedBy))
            .ToListAsync(cancellationToken);

        var milkYields = (await milkYieldsReader.GetByAnimalIdAsync(animal.Id, cancellationToken))
            .Select(y => new AnimalMilkYieldSummaryDto(y.Id, y.Date, y.Shift, y.Liters))
            .ToList();

        return new AnimalDto(
            animal.Id,
            animal.SpeciesId,
            animal.BreedId,
            animal.CategoryId,
            animal.Sex,
            animal.BirthDate,
            animal.Identifiers
                .Select(i => new AnimalIdentifierDto(i.Type, i.Value, i.ValidFrom, i.ValidTo))
                .ToList(),
            FirstActiveIdentifier(animal, IdentifierType.FarmTag),
            FirstActiveIdentifier(animal, IdentifierType.OfficialTag),
            FirstActiveIdentifier(animal, IdentifierType.Name),
            speciesName,
            breedName,
            categoryName,
            hasDisposal ? "Disposed" : "Active",
            withdrawalUntil.HasValue,
            withdrawalUntil,
            events,
            milkYields,
            animal.MotherId,
            animal.FatherAnimalId,
            animal.FatherStrawId,
            animal.BirthingId);
    }

    private static string? FirstActiveIdentifier(Animal animal, IdentifierType type) =>
        animal.Identifiers.Where(i => i.Type == type && i.IsActive).Select(i => i.Value).FirstOrDefault();
}

using Hato.Modules.Livestock.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.Animals;

/// <summary>
/// One field that differed between what was already stored and what this write wanted.
/// Emitted only when a genuine conflict window existed; a routine edit — including a
/// resend of the exact same values — produces none of these.
/// </summary>
public record FieldConflict(string FieldName, string? ServerValue, string? AttemptedValue);

public record UpdateAnimalResult(Guid AnimalId, bool HadConflict, bool ClientChangesApplied, List<FieldConflict> Conflicts);

/// <summary>
/// Edits an animal's mutable fields — the "Datos de Animales" ADR-0008 names as the
/// editable-entity example for LWW.
///
/// <paramref name="CheckForConflicts"/> distinguishes two callers with genuinely different
/// guarantees. A direct panel edit is synchronous and online: whatever it sees is the
/// truth at that instant, so it always applies (the default, <c>false</c>). A sync push
/// declares <paramref name="KnownUpdatedAt"/> — the animal's <see cref="Domain.Animal.LastEditedAt"/>
/// as this device last saw it, <c>null</c> meaning "I saw it had never been edited" — and
/// that claim can go stale while the device was offline. When the row's actual baseline no
/// longer matches what the device claims to have seen, two writes are competing for the
/// same field, and LWW decides by comparing <paramref name="OccurredAt"/> — the moment
/// this edit actually happened in the field — against that newer baseline.
/// </summary>
public record UpdateAnimalCommand(
    Guid AnimalId,
    Guid? BreedId,
    Guid? CategoryId,
    DateOnly? BirthDate,
    DateTimeOffset OccurredAt,
    bool CheckForConflicts = false,
    DateTimeOffset? KnownUpdatedAt = null,
    Guid? ClientOperationId = null,
    string? DeviceId = null) : IRequest<UpdateAnimalResult>;

public class UpdateAnimalHandler(ILivestockDbContext dbContext) : IRequestHandler<UpdateAnimalCommand, UpdateAnimalResult>
{
    public async Task<UpdateAnimalResult> Handle(UpdateAnimalCommand request, CancellationToken cancellationToken)
    {
        var animal = await dbContext.Animals.FirstOrDefaultAsync(a => a.Id == request.AnimalId, cancellationToken)
            ?? throw new KeyNotFoundException($"El animal con ID '{request.AnimalId}' no existe.");

        var baseline = animal.LastEditedAt;
        var hasConflict = request.CheckForConflicts && IsStale(request.KnownUpdatedAt, baseline);

        var conflicts = hasConflict
            ? DetectFieldConflicts(request, animal.BreedId, animal.CategoryId, animal.BirthDate)
            : [];

        // Whichever edit actually happened later in the field wins, regardless of the
        // order the two pushes arrived in. With no conflict, the edit always applies —
        // there is nothing to compare it against.
        var clientWins = !hasConflict || request.OccurredAt > baseline!.Value;

        if (clientWins)
        {
            animal.Update(request.BreedId, request.CategoryId, request.BirthDate, request.OccurredAt);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new UpdateAnimalResult(animal.Id, hasConflict, clientWins, conflicts);
    }

    /// <summary>
    /// True when the row has moved on from what the device claims to have last seen. Both
    /// sides agreeing "never edited" (both null) is not staleness — it is the normal state
    /// before any edit has ever happened.
    /// </summary>
    private static bool IsStale(DateTimeOffset? known, DateTimeOffset? actualBaseline)
    {
        if (known == actualBaseline) return false;
        if (known is null) return actualBaseline is not null;
        if (actualBaseline is null) return false;

        return known.Value < actualBaseline.Value;
    }

    private static List<FieldConflict> DetectFieldConflicts(
        UpdateAnimalCommand request, Guid? currentBreedId, Guid? currentCategoryId, DateOnly? currentBirthDate)
    {
        var conflicts = new List<FieldConflict>();

        if (request.BreedId != currentBreedId)
            conflicts.Add(new FieldConflict(nameof(Domain.Animal.BreedId), currentBreedId?.ToString(), request.BreedId?.ToString()));

        if (request.CategoryId != currentCategoryId)
            conflicts.Add(new FieldConflict(nameof(Domain.Animal.CategoryId), currentCategoryId?.ToString(), request.CategoryId?.ToString()));

        if (request.BirthDate != currentBirthDate)
            conflicts.Add(new FieldConflict(nameof(Domain.Animal.BirthDate), currentBirthDate?.ToString("O"), request.BirthDate?.ToString("O")));

        return conflicts;
    }
}

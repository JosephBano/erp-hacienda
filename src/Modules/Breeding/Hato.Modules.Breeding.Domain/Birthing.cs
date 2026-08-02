using Hato.SharedKernel;
using Hato.Modules.Breeding.Domain.Enums;
using Hato.Modules.Breeding.Domain.Events;

namespace Hato.Modules.Breeding.Domain;

public class Birthing : AuditableEntity
{
    public Guid DamId { get; private set; }
    public Guid? PregnancyId { get; private set; }
    public DateOnly BirthDate { get; private set; }
    public BirthingDifficulty Difficulty { get; private set; }
    public int TotalBorn { get; private set; }
    public int BornAlive { get; private set; }
    public int BornDead { get; private set; }
    public int Mummified { get; private set; }
    public decimal? LitterWeight { get; private set; }
    public string? Notes { get; private set; }
    public DateOnly? WeanedAt { get; private set; }
    public int? WeanedCount { get; private set; }

    private Birthing() { } // EF Core

    public static Birthing Create(
        Guid damId,
        DateOnly birthDate,
        BirthingDifficulty difficulty,
        int bornAlive,
        int bornDead = 0,
        int mummified = 0,
        Guid? pregnancyId = null,
        decimal? litterWeight = null,
        string? notes = null,
        Guid? sireAnimalId = null,
        Guid? fatherStrawId = null,
        List<OffspringBirthInfo>? offspring = null)
    {
        if (damId == Guid.Empty)
            throw new DomainException("Dam ID is required for a birthing event.");
        if (bornAlive < 0 || bornDead < 0 || mummified < 0)
            throw new DomainException("Born counts cannot be negative.");

        int totalBorn = bornAlive + bornDead + mummified;
        if (totalBorn == 0)
            throw new DomainException("Total born count must be greater than zero.");

        var birthing = new Birthing
        {
            Id = Guid.NewGuid(),
            DamId = damId,
            PregnancyId = pregnancyId,
            BirthDate = birthDate,
            Difficulty = difficulty,
            TotalBorn = totalBorn,
            BornAlive = bornAlive,
            BornDead = bornDead,
            Mummified = mummified,
            LitterWeight = litterWeight,
            Notes = notes?.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        birthing.RaiseDomainEvent(new BirthingRecordedEvent(
            birthing.Id,
            birthing.DamId,
            birthing.PregnancyId,
            birthing.BirthDate,
            sireAnimalId,
            fatherStrawId,
            offspring ?? []
        ));

        return birthing;
    }

    public void RecordWeaning(DateOnly weaningDate, int weanedCount, string? notes = null)
    {
        if (WeanedAt is not null)
            throw new DomainException("Este parto ya tiene un destete registrado.");
        if (weaningDate < BirthDate)
            throw new DomainException("La fecha de destete no puede ser anterior a la fecha de parto.");
        if (weanedCount < 0 || weanedCount > BornAlive)
            throw new DomainException("La cantidad destetada no puede ser mayor a las crías nacidas vivas.");

        WeanedAt = weaningDate;
        WeanedCount = weanedCount;
        if (!string.IsNullOrWhiteSpace(notes))
            Notes = string.IsNullOrWhiteSpace(Notes) ? notes.Trim() : $"{Notes} | Destete: {notes.Trim()}";
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

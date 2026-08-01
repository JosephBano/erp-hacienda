using Hato.SharedKernel;
using Hato.Modules.Breeding.Domain.Enums;
using Hato.Modules.Breeding.Domain.Events;

namespace Hato.Modules.Breeding.Domain;

public class BreedingService : AuditableEntity
{
    public Guid DamId { get; private set; }
    public ServiceType ServiceType { get; private set; }
    public Guid? SireAnimalId { get; private set; }
    public Guid? StrawId { get; private set; }
    public DateOnly ServiceDate { get; private set; }
    public string? Technician { get; private set; }
    public string? Notes { get; private set; }
    public decimal? BodyConditionScore { get; private set; }

    private BreedingService() { } // EF Core

    public static BreedingService Create(
        Guid damId,
        ServiceType serviceType,
        DateOnly serviceDate,
        Guid? sireAnimalId = null,
        Guid? strawId = null,
        string? technician = null,
        string? notes = null,
        decimal? bodyConditionScore = null)
    {
        if (damId == Guid.Empty)
            throw new DomainException("Dam ID is required for a breeding service.");

        // XOR invariant: SireAnimalId XOR StrawId
        if (sireAnimalId.HasValue && strawId.HasValue)
            throw new DomainException("A service cannot have both a sire animal and a semen straw.");
        if (!sireAnimalId.HasValue && !strawId.HasValue)
            throw new DomainException("A service must specify either a sire animal or a semen straw.");

        if (serviceType == ServiceType.Natural && strawId.HasValue)
            throw new DomainException("Natural service cannot use a semen straw.");
        if (serviceType == ServiceType.ArtificialInsemination && sireAnimalId.HasValue)
            throw new DomainException("Artificial insemination cannot use a sire animal directly.");

        var service = new BreedingService
        {
            Id = Guid.NewGuid(),
            DamId = damId,
            ServiceType = serviceType,
            ServiceDate = serviceDate,
            SireAnimalId = sireAnimalId,
            StrawId = strawId,
            Technician = technician?.Trim(),
            Notes = notes?.Trim(),
            BodyConditionScore = bodyConditionScore,
            CreatedAt = DateTimeOffset.UtcNow
        };

        service.RaiseDomainEvent(new BreedingServiceRegisteredEvent(
            service.Id,
            service.DamId,
            service.ServiceType,
            service.SireAnimalId,
            service.StrawId,
            service.ServiceDate
        ));

        return service;
    }
}

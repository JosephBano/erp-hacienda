using Hato.Modules.Breeding.Domain;
using Hato.Modules.Breeding.Domain.Enums;
using Hato.SharedKernel;
using Xunit;

namespace Hato.Modules.Breeding.UnitTests.Domain;

public class BreedingServiceTests
{
    [Fact]
    public void Create_NaturalService_WithSireAnimal_Succeeds()
    {
        var damId = Guid.NewGuid();
        var sireId = Guid.NewGuid();
        var serviceDate = new DateOnly(2026, 8, 1);

        var service = BreedingService.Create(damId, ServiceType.Natural, serviceDate, sireAnimalId: sireId);

        Assert.NotNull(service);
        Assert.Equal(damId, service.DamId);
        Assert.Equal(sireId, service.SireAnimalId);
        Assert.Null(service.StrawId);
        Assert.Single(service.DomainEvents);
    }

    [Fact]
    public void Create_AI_WithStraw_Succeeds()
    {
        var damId = Guid.NewGuid();
        var strawId = Guid.NewGuid();
        var serviceDate = new DateOnly(2026, 8, 1);

        var service = BreedingService.Create(damId, ServiceType.ArtificialInsemination, serviceDate, strawId: strawId);

        Assert.NotNull(service);
        Assert.Equal(strawId, service.StrawId);
        Assert.Null(service.SireAnimalId);
    }

    [Fact]
    public void Create_WithBothSireAndStraw_ThrowsDomainException()
    {
        var damId = Guid.NewGuid();
        var sireId = Guid.NewGuid();
        var strawId = Guid.NewGuid();
        var serviceDate = new DateOnly(2026, 8, 1);

        var ex = Assert.Throws<DomainException>(() =>
            BreedingService.Create(damId, ServiceType.ArtificialInsemination, serviceDate, sireAnimalId: sireId, strawId: strawId));

        Assert.Contains("cannot have both a sire animal and a semen straw", ex.Message);
    }

    [Fact]
    public void Create_WithoutSireOrStraw_ThrowsDomainException()
    {
        var damId = Guid.NewGuid();
        var serviceDate = new DateOnly(2026, 8, 1);

        var ex = Assert.Throws<DomainException>(() =>
            BreedingService.Create(damId, ServiceType.Natural, serviceDate));

        Assert.Contains("must specify either a sire animal or a semen straw", ex.Message);
    }
}

using Hato.Modules.Breeding.Domain;
using Hato.Modules.Breeding.Domain.Enums;
using Hato.Modules.Breeding.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Hato.TestSupport;
using Xunit;

namespace Hato.Modules.Breeding.IntegrationTests.Persistence;

public sealed class BreedingDbContextTests : IAsyncLifetime
{
    private TestDatabase _database = null!;

    public async Task InitializeAsync() => _database = await TestDatabase.StartAsync();

    public Task DisposeAsync() => _database.DisposeAsync().AsTask();

    [Fact]
    public async Task Migrations_ApplyCleanly_OnEmptyDatabase()
    {
        var options = new DbContextOptionsBuilder<BreedingDbContext>()
            .UseNpgsql(_database.ConnectionString)
            .Options;

        await using var context = new BreedingDbContext(options);

        await context.Database.MigrateAsync();

        Assert.True(await context.Database.CanConnectAsync());
    }

    [Fact]
    public async Task CanInsertAndQueryBreedingService_WithPostgreSQL()
    {
        var options = new DbContextOptionsBuilder<BreedingDbContext>()
            .UseNpgsql(_database.ConnectionString)
            .Options;

        await using var context = new BreedingDbContext(options);
        await context.Database.MigrateAsync();

        var damId = Guid.NewGuid();
        var sireId = Guid.NewGuid();
        var service = BreedingService.Create(damId, ServiceType.Natural, new DateOnly(2026, 8, 1), sireAnimalId: sireId);

        context.BreedingServices.Add(service);
        await context.SaveChangesAsync();

        var saved = await context.BreedingServices.FirstOrDefaultAsync(s => s.Id == service.Id);
        Assert.NotNull(saved);
        Assert.Equal(damId, saved.DamId);
        Assert.Equal(sireId, saved.SireAnimalId);
    }
}

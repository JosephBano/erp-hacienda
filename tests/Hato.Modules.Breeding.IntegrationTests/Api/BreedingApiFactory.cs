using Hato.Modules.Breeding.Infrastructure.Persistence;
using Hato.Modules.Livestock.Infrastructure.Persistence;
using Hato.Modules.Production.Infrastructure.Persistence;
using Hato.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hato.Modules.Breeding.IntegrationTests.Api;

public class BreedingApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Real PostgreSQL, resolved by TestSupport: a throwaway container by default,
    // or a fresh database on the server named by HATO_TEST_POSTGRES.
    private TestDatabase _database = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:HatoDb"] = _database.ConnectionString,
            });
        });

        builder.ConfigureTestServices(services => services.AddTestAuthentication());
    }

    public async Task InitializeAsync()
    {
        _database = await TestDatabase.StartAsync();

        var livestockOptions = new DbContextOptionsBuilder<LivestockDbContext>()
            .UseNpgsql(_database.ConnectionString, n => n.MigrationsHistoryTable("__ef_migrations_history", LivestockDbContext.Schema))
            .Options;
        await using var livestockContext = new LivestockDbContext(livestockOptions);
        await livestockContext.Database.MigrateAsync();

        var productionOptions = new DbContextOptionsBuilder<ProductionDbContext>()
            .UseNpgsql(_database.ConnectionString, n => n.MigrationsHistoryTable("__ef_migrations_history", ProductionDbContext.Schema))
            .Options;
        await using var productionContext = new ProductionDbContext(productionOptions);
        await productionContext.Database.MigrateAsync();

        var breedingOptions = new DbContextOptionsBuilder<BreedingDbContext>()
            .UseNpgsql(_database.ConnectionString, n => n.MigrationsHistoryTable("__ef_migrations_history", BreedingDbContext.Schema))
            .Options;
        await using var breedingContext = new BreedingDbContext(breedingOptions);
        await breedingContext.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }
}

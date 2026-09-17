using Hato.Modules.Inventory.Infrastructure.Persistence;
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

namespace Hato.Modules.Inventory.IntegrationTests;

public class InventoryApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Real PostgreSQL, resolved by TestSupport: a throwaway container by default,
    // or a fresh database on the server named by HATO_TEST_POSTGRES.
    private TestDatabase _database = null!;

    /// <summary>Exposed so subclasses that override <c>ConfigureWebHost</c> can reuse the connection string.</summary>
    protected string ConnectionString => _database.ConnectionString;

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

        var inventoryOptions = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseNpgsql(_database.ConnectionString, n => n.MigrationsHistoryTable("__ef_migrations_history", InventoryDbContext.Schema))
            .Options;
        await using var inventoryContext = new InventoryDbContext(inventoryOptions);
        await inventoryContext.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }
}

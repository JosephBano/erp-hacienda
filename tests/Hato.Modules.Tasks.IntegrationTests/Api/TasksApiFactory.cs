using Hato.Modules.Breeding.Infrastructure.Persistence;
using Hato.Modules.Inventory.Infrastructure.Persistence;
using Hato.Modules.Livestock.Infrastructure.Persistence;
using Hato.Modules.Tasks.Infrastructure.Persistence;
using Hato.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Hato.Modules.Tasks.IntegrationTests.Api;

/// <summary>
/// Boots the real host with every module the alert generators read from (Livestock,
/// Breeding, Inventory, Tasks) — GenerateAlertsCommand previously had zero test coverage,
/// which is exactly how a raw-SQL column-name bug shipped undetected.
/// </summary>
public class TasksApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:HatoDb"] = _postgres.GetConnectionString(),
            });
        });

        builder.ConfigureTestServices(services => services.AddTestAuthentication());
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var livestockOptions = new DbContextOptionsBuilder<LivestockDbContext>()
            .UseNpgsql(_postgres.GetConnectionString(), n => n.MigrationsHistoryTable("__ef_migrations_history", LivestockDbContext.Schema))
            .Options;
        await using var livestockContext = new LivestockDbContext(livestockOptions);
        await livestockContext.Database.MigrateAsync();

        var breedingOptions = new DbContextOptionsBuilder<BreedingDbContext>()
            .UseNpgsql(_postgres.GetConnectionString(), n => n.MigrationsHistoryTable("__ef_migrations_history", BreedingDbContext.Schema))
            .Options;
        await using var breedingContext = new BreedingDbContext(breedingOptions);
        await breedingContext.Database.MigrateAsync();

        var inventoryOptions = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseNpgsql(_postgres.GetConnectionString(), n => n.MigrationsHistoryTable("__ef_migrations_history", InventoryDbContext.Schema))
            .Options;
        await using var inventoryContext = new InventoryDbContext(inventoryOptions);
        await inventoryContext.Database.MigrateAsync();

        var tasksOptions = new DbContextOptionsBuilder<TasksDbContext>()
            .UseNpgsql(_postgres.GetConnectionString(), n => n.MigrationsHistoryTable("__ef_migrations_history", TasksDbContext.Schema))
            .Options;
        await using var tasksContext = new TasksDbContext(tasksOptions);
        await tasksContext.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

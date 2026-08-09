using Hato.Modules.Breeding.Infrastructure.Persistence;
using Hato.Modules.Inventory.Infrastructure.Persistence;
using Hato.Modules.Livestock.Infrastructure.Persistence;
using Hato.Modules.People.Infrastructure.Persistence;
using Hato.Modules.Production.Infrastructure.Persistence;
using Hato.Modules.Tasks.Infrastructure.Persistence;
using Hato.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Hato.Sync.IntegrationTests;

/// <summary>
/// Shared PostgreSQL container for the whole synchronization suite. The database is
/// always created from scratch by running the migrations (PLAN-FASE-3-4 sec.2.1), never
/// by <c>EnsureCreated</c>: a migration that does not apply must fail the suite.
/// </summary>
public class SyncApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Real PostgreSQL, resolved by TestSupport: a throwaway container by default,
    // or a fresh database on the server named by HATO_TEST_POSTGRES.
    private TestDatabase _database = null!;

    public string ConnectionString => _database.ConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:HatoDb"] = _database.ConnectionString,
            });
        });
    }

    public async Task InitializeAsync()
    {
        _database = await TestDatabase.StartAsync();

        await MigrateAsync<LivestockDbContext>(LivestockDbContext.Schema, o => new LivestockDbContext(o));
        await MigrateAsync<ProductionDbContext>(ProductionDbContext.Schema, o => new ProductionDbContext(o));
        await MigrateAsync<InventoryDbContext>(InventoryDbContext.Schema, o => new InventoryDbContext(o));
        await MigrateAsync<BreedingDbContext>(BreedingDbContext.Schema, o => new BreedingDbContext(o));
        await MigrateAsync<TasksDbContext>(TasksDbContext.Schema, o => new TasksDbContext(o));
        await MigrateAsync<PeopleDbContext>(PeopleDbContext.Schema, o => new PeopleDbContext(o));
    }

    private async Task MigrateAsync<TContext>(
        string schema,
        Func<DbContextOptions<TContext>, TContext> build)
        where TContext : DbContext
    {
        var options = new DbContextOptionsBuilder<TContext>()
            .UseNpgsql(_database.ConnectionString,
                n => n.MigrationsHistoryTable("__ef_migrations_history", schema))
            .Options;

        await using var context = build(options);
        await context.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public class SyncCollection : ICollectionFixture<SyncApiFactory>
{
    public const string Name = "sync";
}

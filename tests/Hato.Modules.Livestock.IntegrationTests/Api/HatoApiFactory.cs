using Hato.Modules.Livestock.Infrastructure.Persistence;
using Hato.Modules.People.Infrastructure.Persistence;
using Hato.Modules.Production.Infrastructure.Persistence;
using Hato.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hato.Modules.Livestock.IntegrationTests.Api;

/// <summary>
/// Boots the real ASP.NET Core host against a real, ephemeral PostgreSQL (Art. 12: no
/// InMemory provider). The container starts and is migrated before any test runs.
///
/// Every request is authenticated as Admin via <see cref="TestAuthHandler"/> — that
/// keeps the rest of the suite simple. Tests that need real JWT auth (e.g. ADR-0025
/// PR2 403-permission tests) derive from this class and override <c>ConfigureWebHost</c>
/// to skip the TestAuth registration.
/// </summary>
public class HatoApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
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

        var options = new DbContextOptionsBuilder<LivestockDbContext>()
            .UseNpgsql(_database.ConnectionString)
            .Options;

        await using var context = new LivestockDbContext(options);
        await context.Database.MigrateAsync();

        // GetAnimalByIdQuery reads milk yields through Production's public contract
        // (Art. 6), so the animal detail screen needs that schema present too.
        var productionOptions = new DbContextOptionsBuilder<ProductionDbContext>()
            .UseNpgsql(_database.ConnectionString, n => n.MigrationsHistoryTable("__ef_migrations_history", ProductionDbContext.Schema))
            .Options;
        await using var productionContext = new ProductionDbContext(productionOptions);
        await productionContext.Database.MigrateAsync();

        // ADR-0025 PR2: 403-permission tests need to bootstrap users and roles via the
        // People endpoints. Migrating the schema here means every Livestock integration
        // test gets People available — a small constant overhead, but it removes the
        // need for a second factory in the suite.
        var peopleOptions = new DbContextOptionsBuilder<PeopleDbContext>()
            .UseNpgsql(_database.ConnectionString, n => n.MigrationsHistoryTable("__ef_migrations_history", PeopleDbContext.Schema))
            .Options;
        await using var peopleContext = new PeopleDbContext(peopleOptions);
        await peopleContext.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }
}

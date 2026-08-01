using Hato.Modules.Livestock.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Hato.Modules.Livestock.IntegrationTests.Api;

/// <summary>
/// Boots the real ASP.NET Core host against a real, ephemeral PostgreSQL (Art. 12: no
/// InMemory provider). The container starts and is migrated before any test runs.
/// </summary>
public class HatoApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
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
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<LivestockDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        await using var context = new LivestockDbContext(options);
        await context.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

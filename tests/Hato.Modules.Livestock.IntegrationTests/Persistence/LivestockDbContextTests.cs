using Hato.Modules.Livestock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Hato.Modules.Livestock.IntegrationTests.Persistence;

/// <summary>
/// Integration tests run against a real PostgreSQL (Art. 12: no InMemory provider
/// to verify final behavior).
/// </summary>
public sealed class LivestockDbContextTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task Migrations_ApplyCleanly_OnEmptyDatabase()
    {
        var options = new DbContextOptionsBuilder<LivestockDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        await using var context = new LivestockDbContext(options);

        await context.Database.MigrateAsync();

        Assert.True(await context.Database.CanConnectAsync());
    }
}

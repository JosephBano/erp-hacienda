using Hato.Modules.Livestock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Hato.TestSupport;

namespace Hato.Modules.Livestock.IntegrationTests.Persistence;

/// <summary>
/// Integration tests run against a real PostgreSQL (Art. 12: no InMemory provider
/// to verify final behavior).
/// </summary>
public sealed class LivestockDbContextTests : IAsyncLifetime
{
    private TestDatabase _database = null!;

    public async Task InitializeAsync() => _database = await TestDatabase.StartAsync();

    public Task DisposeAsync() => _database.DisposeAsync().AsTask();

    [Fact]
    public async Task Migrations_ApplyCleanly_OnEmptyDatabase()
    {
        var options = new DbContextOptionsBuilder<LivestockDbContext>()
            .UseNpgsql(_database.ConnectionString)
            .Options;

        await using var context = new LivestockDbContext(options);

        await context.Database.MigrateAsync();

        Assert.True(await context.Database.CanConnectAsync());
    }
}

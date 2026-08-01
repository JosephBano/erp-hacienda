using Hato.Modules.Tasks.Domain;
using Hato.Modules.Tasks.Domain.Enums;
using Hato.Modules.Tasks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Hato.Modules.Tasks.IntegrationTests.Persistence;

public sealed class TasksDbContextTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task Migrations_ApplyCleanly_OnEmptyDatabase()
    {
        var options = new DbContextOptionsBuilder<TasksDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        await using var context = new TasksDbContext(options);

        await context.Database.MigrateAsync();

        Assert.True(await context.Database.CanConnectAsync());
    }

    [Fact]
    public async Task CanInsertAndQueryAlert_WithPostgreSQL()
    {
        var options = new DbContextOptionsBuilder<TasksDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        await using var context = new TasksDbContext(options);
        await context.Database.MigrateAsync();

        var alert = Alert.Create("TEST_ALERT", "Test Title", "Test Message", AlertSeverity.Warning);
        context.Alerts.Add(alert);
        await context.SaveChangesAsync();

        var saved = await context.Alerts.FirstOrDefaultAsync(a => a.Id == alert.Id);
        Assert.NotNull(saved);
        Assert.Equal("TEST_ALERT", saved.Code);
        Assert.False(saved.IsDismissed);
    }
}

using Hato.Modules.Breeding.Infrastructure.Persistence;
using Hato.Modules.Delivery.Application.Abstractions;
using Hato.Modules.Delivery.Domain;
using Hato.Modules.Delivery.Infrastructure.Persistence;
using Hato.Modules.Delivery.Infrastructure.Persistence.Services;
using Hato.Modules.Inventory.Infrastructure.Persistence;
using Hato.Modules.Livestock.Infrastructure.Persistence;
using Hato.Modules.People.Infrastructure.Persistence;
using Hato.Modules.Production.Infrastructure.Persistence;
using Hato.Modules.Tasks.Infrastructure.Persistence;
using Hato.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hato.Modules.Delivery.IntegrationTests;

public class DeliveryApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private TestDatabase _database = null!;
    private readonly string _tempArtifactDirectory = Path.Combine(Path.GetTempPath(), "hato_delivery_it_" + Guid.NewGuid().ToString("N"));

    public string ArtifactDirectory => _tempArtifactDirectory;
    public string ConnectionString => _database.ConnectionString;
    public FakeGitHubActionsClient GitHubClient { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_tempArtifactDirectory);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:HatoDb"] = _database.ConnectionString,
                ["Delivery:ArtifactStoragePath"] = _tempArtifactDirectory,
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.AddTestAuthentication();
            // Ensure IArtifactStorage points to test directory
            services.AddSingleton<IArtifactStorage>(new LocalArtifactStorage(_tempArtifactDirectory));
            services.AddSingleton<IGitHubActionsClient>(GitHubClient);
        });
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

        var breedingOptions = new DbContextOptionsBuilder<BreedingDbContext>()
            .UseNpgsql(_database.ConnectionString, n => n.MigrationsHistoryTable("__ef_migrations_history", BreedingDbContext.Schema))
            .Options;
        await using var breedingContext = new BreedingDbContext(breedingOptions);
        await breedingContext.Database.MigrateAsync();

        var peopleOptions = new DbContextOptionsBuilder<PeopleDbContext>()
            .UseNpgsql(_database.ConnectionString, n => n.MigrationsHistoryTable("__ef_migrations_history", PeopleDbContext.Schema))
            .Options;
        await using var peopleContext = new PeopleDbContext(peopleOptions);
        await peopleContext.Database.MigrateAsync();

        var tasksOptions = new DbContextOptionsBuilder<TasksDbContext>()
            .UseNpgsql(_database.ConnectionString, n => n.MigrationsHistoryTable("__ef_migrations_history", TasksDbContext.Schema))
            .Options;
        await using var tasksContext = new TasksDbContext(tasksOptions);
        await tasksContext.Database.MigrateAsync();

        var deliveryOptions = new DbContextOptionsBuilder<DeliveryDbContext>()
            .UseNpgsql(_database.ConnectionString, n => n.MigrationsHistoryTable("__ef_migrations_history", DeliveryDbContext.Schema))
            .Options;
        await using var deliveryContext = new DeliveryDbContext(deliveryOptions);
        await deliveryContext.Database.MigrateAsync();
    }

    public async Task ExecuteDbContextAsync(Func<DeliveryDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DeliveryDbContext>();
        await action(context);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        if (Directory.Exists(_tempArtifactDirectory))
        {
            try
            {
                Directory.Delete(_tempArtifactDirectory, true);
            }
            catch
            {
                // ignore cleanup errors
            }
        }
        await _database.DisposeAsync();
    }
}

public class FakeGitHubActionsClient : IGitHubActionsClient
{
    public List<MobileBuildRequest> DispatchedRequests { get; } = [];
    public List<GitHubWorkflowRunDto> Runs { get; set; } = [];
    public Func<long, string, Stream?>? ArtifactStreamFactory { get; set; }

    public Task<bool> DispatchWorkflowAsync(MobileBuildRequest request, CancellationToken cancellationToken = default)
    {
        DispatchedRequests.Add(request);
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<GitHubWorkflowRunDto>> ListWorkflowRunsAsync(string workflowFileName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<GitHubWorkflowRunDto>>(Runs);
    }

    public Task<Stream?> DownloadArtifactAsync(long runId, string artifactName, CancellationToken cancellationToken = default)
    {
        if (ArtifactStreamFactory != null)
            return Task.FromResult(ArtifactStreamFactory(runId, artifactName));
        return Task.FromResult<Stream?>(null);
    }
}

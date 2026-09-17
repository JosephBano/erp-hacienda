using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Hato.Modules.Livestock.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by `dotnet ef`. The EF tools prefer this factory over
/// Hato.Api's own DI-configured DbContext for every command — including `database
/// update` — so it must read the real connection string from deployment environment
/// variables or user secrets rather than a hardcoded one, or `database update` would
/// silently target the wrong credentials. `migrations add` never connects, so it falls
/// back to a placeholder when no connection string is configured.
/// </summary>
public class LivestockDbContextFactory : IDesignTimeDbContextFactory<LivestockDbContext>
{
    private const string HatoApiUserSecretsId = "44de5e62-c571-4b2e-89a6-be7abf5e3dee";

    public LivestockDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(HatoApiUserSecretsId)
            .Build();

        // No credentials here on purpose: `migrations add` only needs a provider
        // configured, it never opens a connection. `database update` resolves its
        // credentials from the deployment environment or user secrets.
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__HatoDb")
            ?? Environment.GetEnvironmentVariable("CONNECTION_STRING")
            ?? configuration.GetConnectionString("HatoDb")
            ?? "Host=localhost;Database=hato_migrations_design_time_only";

        var optionsBuilder = new DbContextOptionsBuilder<LivestockDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new LivestockDbContext(optionsBuilder.Options);
    }
}

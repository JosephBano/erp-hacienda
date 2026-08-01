using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Hato.Modules.Inventory.Infrastructure.Persistence;

public class InventoryDbContextFactory : IDesignTimeDbContextFactory<InventoryDbContext>
{
    private const string HatoApiUserSecretsId = "44de5e62-c571-4b2e-89a6-be7abf5e3dee";

    public InventoryDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(HatoApiUserSecretsId)
            .Build();

        var connectionString = configuration.GetConnectionString("HatoDb")
            ?? "Host=localhost;Database=hato_migrations_design_time_only";

        var optionsBuilder = new DbContextOptionsBuilder<InventoryDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new InventoryDbContext(optionsBuilder.Options);
    }
}

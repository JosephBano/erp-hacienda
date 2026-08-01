using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Hato.Modules.Production.Infrastructure.Persistence;

public class ProductionDbContextFactory : IDesignTimeDbContextFactory<ProductionDbContext>
{
    private const string HatoApiUserSecretsId = "44de5e62-c571-4b2e-89a6-be7abf5e3dee";

    public ProductionDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(HatoApiUserSecretsId)
            .Build();

        var connectionString = configuration.GetConnectionString("HatoDb")
            ?? "Host=localhost;Database=hato_migrations_design_time_only";

        var optionsBuilder = new DbContextOptionsBuilder<ProductionDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new ProductionDbContext(optionsBuilder.Options);
    }
}

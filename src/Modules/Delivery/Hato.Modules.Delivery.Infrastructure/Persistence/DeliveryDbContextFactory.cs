using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Hato.Modules.Delivery.Infrastructure.Persistence;

public class DeliveryDbContextFactory : IDesignTimeDbContextFactory<DeliveryDbContext>
{
    private const string HatoApiUserSecretsId = "44de5e62-c571-4b2e-89a6-be7abf5e3dee";

    public DeliveryDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(HatoApiUserSecretsId)
            .Build();

        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__HatoDb")
            ?? Environment.GetEnvironmentVariable("CONNECTION_STRING")
            ?? configuration.GetConnectionString("HatoDb")
            ?? "Host=localhost;Database=hato_migrations_design_time_only";

        var optionsBuilder = new DbContextOptionsBuilder<DeliveryDbContext>();
        optionsBuilder.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", DeliveryDbContext.Schema));

        return new DeliveryDbContext(optionsBuilder.Options);
    }
}

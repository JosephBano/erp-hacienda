using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Hato.Modules.People.Infrastructure.Persistence;

public class PeopleDbContextFactory : IDesignTimeDbContextFactory<PeopleDbContext>
{
    private const string HatoApiUserSecretsId = "44de5e62-c571-4b2e-89a6-be7abf5e3dee";

    public PeopleDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(HatoApiUserSecretsId)
            .Build();

        var connectionString = configuration.GetConnectionString("HatoDb")
            ?? "Host=localhost;Database=hato_migrations_design_time_only";

        var optionsBuilder = new DbContextOptionsBuilder<PeopleDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new PeopleDbContext(optionsBuilder.Options);
    }
}

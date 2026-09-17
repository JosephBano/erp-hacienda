using Hato.Modules.Inventory.Infrastructure.Persistence;
using Hato.Modules.Livestock.Infrastructure.Persistence;
using Hato.Modules.People.Infrastructure.Persistence;
using Hato.Modules.Production.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Hato.Modules.People.IntegrationTests;

public sealed class DesignTimeDbContextFactoryEnvironmentTests
{
    [Fact]
    public void Factories_UseConnectionStringFromEnvironment()
    {
        const string connectionString = "Host=postgres;Port=5432;Database=hato;Username=migrator;Password=not-a-real-secret";
        var previous = Environment.GetEnvironmentVariable("ConnectionStrings__HatoDb");

        try
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__HatoDb", connectionString);

            Assert.Equal(connectionString, new InventoryDbContextFactory().CreateDbContext([]).Database.GetConnectionString());
            Assert.Equal(connectionString, new LivestockDbContextFactory().CreateDbContext([]).Database.GetConnectionString());
            Assert.Equal(connectionString, new PeopleDbContextFactory().CreateDbContext([]).Database.GetConnectionString());
            Assert.Equal(connectionString, new ProductionDbContextFactory().CreateDbContext([]).Database.GetConnectionString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__HatoDb", previous);
        }
    }
}

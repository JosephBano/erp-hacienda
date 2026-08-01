using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hato.Modules.Production.Infrastructure.Persistence;

public class ProductionDbContextFactory : IDesignTimeDbContextFactory<ProductionDbContext>
{
    public ProductionDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ProductionDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=hato_dev;Username=hato;Password=hato_dev_secret");

        return new ProductionDbContext(optionsBuilder.Options);
    }
}

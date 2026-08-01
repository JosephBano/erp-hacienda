using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hato.Modules.People.Infrastructure.Persistence;

public class PeopleDbContextFactory : IDesignTimeDbContextFactory<PeopleDbContext>
{
    public PeopleDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PeopleDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=hato_dev;Username=hato;Password=hato_dev_secret");

        return new PeopleDbContext(optionsBuilder.Options);
    }
}

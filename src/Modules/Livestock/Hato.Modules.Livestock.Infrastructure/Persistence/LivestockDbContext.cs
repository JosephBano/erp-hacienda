using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Infrastructure.Persistence;

/// <summary>
/// Livestock module persistence. Each module owns its schema; cross-module table
/// access is forbidden (Art. 6). Schema changes happen only via migrations (Art. 15).
/// </summary>
public class LivestockDbContext(DbContextOptions<LivestockDbContext> options) : DbContext(options)
{
    public const string Schema = "livestock";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LivestockDbContext).Assembly);
    }
}

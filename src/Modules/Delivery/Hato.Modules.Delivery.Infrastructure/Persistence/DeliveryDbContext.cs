using Hato.Modules.Delivery.Application.Abstractions;
using Hato.Modules.Delivery.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Delivery.Infrastructure.Persistence;

public class DeliveryDbContext(DbContextOptions<DeliveryDbContext> options)
    : DbContext(options), IDeliveryDbContext
{
    public const string Schema = "delivery";

    public DbSet<MobileBuildRequest> MobileBuildRequests => Set<MobileBuildRequest>();
    public DbSet<MobileRelease> MobileReleases => Set<MobileRelease>();
    public DbSet<MobileReleaseTransitionAudit> MobileReleaseTransitionAudits => Set<MobileReleaseTransitionAudit>();
    public DbSet<MobileVersionCodeSequence> MobileVersionCodeSequences => Set<MobileVersionCodeSequence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DeliveryDbContext).Assembly);
        ApplySnakeCaseColumnNames(modelBuilder);
    }

    private static void ApplySnakeCaseColumnNames(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
                property.SetColumnName(ToSnakeCase(property.Name));

            foreach (var key in entity.GetKeys())
                key.SetName(ToSnakeCase(key.GetName()!));

            foreach (var foreignKey in entity.GetForeignKeys())
                foreignKey.SetConstraintName(ToSnakeCase(foreignKey.GetConstraintName()!));

            foreach (var index in entity.GetIndexes())
                index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName()!));
        }
    }

    private static string ToSnakeCase(string value) =>
        string.Concat(value.Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + c : c.ToString()))
            .ToLowerInvariant();
}

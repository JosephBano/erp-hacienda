using Hato.Modules.Production.Application.Abstractions;
using Hato.Modules.Production.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Production.Infrastructure.Persistence;

public class ProductionDbContext(DbContextOptions<ProductionDbContext> options)
    : DbContext(options), IProductionDbContext
{
    public const string Schema = "production";

    public DbSet<MilkingSession> MilkingSessions => Set<MilkingSession>();
    public DbSet<MilkYield> MilkYields => Set<MilkYield>();
    public DbSet<Lactation> Lactations => Set<Lactation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProductionDbContext).Assembly);
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

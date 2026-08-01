using Hato.Modules.Breeding.Application.Abstractions;
using Hato.Modules.Breeding.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Breeding.Infrastructure.Persistence;

public class BreedingDbContext(DbContextOptions<BreedingDbContext> options)
    : DbContext(options), IBreedingDbContext
{
    public const string Schema = "breeding";

    public DbSet<SemenStraw> SemenStraws => Set<SemenStraw>();
    public DbSet<BreedingService> BreedingServices => Set<BreedingService>();
    public DbSet<PregnancyCheck> PregnancyChecks => Set<PregnancyCheck>();
    public DbSet<Pregnancy> Pregnancies => Set<Pregnancy>();
    public DbSet<Birthing> Birthings => Set<Birthing>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BreedingDbContext).Assembly);
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

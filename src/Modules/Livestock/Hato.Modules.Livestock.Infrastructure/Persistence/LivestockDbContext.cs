using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Infrastructure.Persistence;

/// <summary>
/// Livestock module persistence. Each module owns its schema; cross-module table
/// access is forbidden (Art. 6). Schema changes happen only via migrations (Art. 15).
/// </summary>
public class LivestockDbContext(DbContextOptions<LivestockDbContext> options)
    : DbContext(options), ILivestockDbContext
{
    public const string Schema = "livestock";

    public DbSet<Species> Species => Set<Species>();
    public DbSet<Breed> Breeds => Set<Breed>();
    public DbSet<AnimalCategory> AnimalCategories => Set<AnimalCategory>();
    public DbSet<Animal> Animals => Set<Animal>();
    public DbSet<AnimalIdentifier> AnimalIdentifiers => Set<AnimalIdentifier>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LivestockDbContext).Assembly);
        ApplySnakeCaseColumnNames(modelBuilder);
    }

    /// <summary>
    /// PostgreSQL naming convention is snake_case (DATA-MODEL.md). Applied here instead
    /// of via a naming-convention package to avoid a dependency that needs an ADR
    /// (AGENTS.md rule 2) for something this small.
    /// </summary>
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

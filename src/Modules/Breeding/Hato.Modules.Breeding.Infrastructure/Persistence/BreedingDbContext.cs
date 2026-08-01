using Hato.Modules.Breeding.Application.Abstractions;
using Hato.Modules.Breeding.Domain;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Breeding.Infrastructure.Persistence;

public class BreedingDbContext(DbContextOptions<BreedingDbContext> options, IPublisher? publisher = null)
    : DbContext(options), IBreedingDbContext
{
    public const string Schema = "breeding";

    public DbSet<SemenStraw> SemenStraws => Set<SemenStraw>();
    public DbSet<BreedingService> BreedingServices => Set<BreedingService>();
    public DbSet<PregnancyCheck> PregnancyChecks => Set<PregnancyCheck>();
    public DbSet<Pregnancy> Pregnancies => Set<Pregnancy>();
    public DbSet<Birthing> Birthings => Set<Birthing>();

    /// <summary>
    /// Publishes each aggregate's raised domain events after a successful save, then
    /// clears them. Without this, events like BirthingRecordedEvent were built and
    /// discarded — nothing downstream ever ran.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entitiesWithEvents = ChangeTracker.Entries<Entity>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count > 0)
            .ToList();

        var result = await base.SaveChangesAsync(cancellationToken);

        if (publisher is not null)
        {
            foreach (var entity in entitiesWithEvents)
            {
                var events = entity.DomainEvents.ToList();
                entity.ClearDomainEvents();
                foreach (var domainEvent in events)
                    await publisher.Publish(domainEvent, cancellationToken);
            }
        }

        return result;
    }

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

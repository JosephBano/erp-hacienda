using Hato.Modules.Livestock.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hato.Modules.Livestock.Infrastructure.Persistence.Configurations;

public class AnimalConfiguration : IEntityTypeConfiguration<Animal>
{
    public void Configure(EntityTypeBuilder<Animal> builder)
    {
        builder.ToTable("animals");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Sex).HasConversion<string>().HasMaxLength(10).IsRequired();

        builder.HasOne<Species>().WithMany().HasForeignKey(a => a.SpeciesId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Breed>().WithMany().HasForeignKey(a => a.BreedId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AnimalCategory>().WithMany().HasForeignKey(a => a.CategoryId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Animal>().WithMany().HasForeignKey(a => a.MotherId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Animal>().WithMany().HasForeignKey(a => a.FatherAnimalId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.MotherId);
        builder.HasIndex(a => a.FatherAnimalId);
        builder.HasIndex(a => a.FatherStrawId);

        // A deleted animal is a tombstone (Art. 1), not an absence — it still exists for
        // the sync pull, which reads with IgnoreQueryFilters() precisely to see past this.
        // Every other query (the regular listing/detail endpoints) must not see it.
        builder.HasQueryFilter(a => a.DeletedAt == null);

        // Identifiers is a read-only projection over the private backing field (Art. 1:
        // history is never exposed as directly mutable outside the aggregate root).
        builder.Metadata.FindNavigation(nameof(Animal.Identifiers))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

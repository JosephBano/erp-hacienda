using Hato.Modules.Livestock.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hato.Modules.Livestock.Infrastructure.Persistence.Configurations;

public class AnimalIdentifierConfiguration : IEntityTypeConfiguration<AnimalIdentifier>
{
    public void Configure(EntityTypeBuilder<AnimalIdentifier> builder)
    {
        builder.ToTable("animal_identifiers");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(i => i.Value).HasMaxLength(100).IsRequired();

        builder.HasOne<Animal>()
            .WithMany(a => a.Identifiers)
            .HasForeignKey(i => i.AnimalId)
            .OnDelete(DeleteBehavior.Restrict);

        // Only one active (valid_to IS NULL) identifier per animal and type — the
        // database backstop for the invariant Animal.AssignIdentifier already enforces
        // in memory (DATA-MODEL.md, Núcleo 1).
        builder.HasIndex(i => new { i.AnimalId, i.Type })
            .IsUnique()
            .HasFilter("valid_to IS NULL");
    }
}

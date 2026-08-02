using Hato.Modules.Breeding.Domain;
using Hato.Modules.Breeding.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hato.Modules.Breeding.Infrastructure.Persistence.Configurations;

public class BreedingServiceConfiguration : IEntityTypeConfiguration<BreedingService>
{
    public void Configure(EntityTypeBuilder<BreedingService> builder)
    {
        builder.ToTable("breeding_services", t =>
        {
            t.HasCheckConstraint("CK_BreedingService_SireOrStraw",
                "(sire_animal_id IS NOT NULL AND straw_id IS NULL) OR (sire_animal_id IS NULL AND straw_id IS NOT NULL)");
        });

        builder.HasKey(s => s.Id);

        builder.Property(s => s.ServiceType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(s => s.Technician).HasMaxLength(100);
        builder.Property(s => s.Notes).HasMaxLength(500);
        builder.Property(s => s.BodyConditionScore).HasPrecision(3, 2);

        builder.HasIndex(s => s.DamId);
        builder.HasIndex(s => s.ServiceDate);
        builder.HasQueryFilter(s => s.DeletedAt == null);
    }
}

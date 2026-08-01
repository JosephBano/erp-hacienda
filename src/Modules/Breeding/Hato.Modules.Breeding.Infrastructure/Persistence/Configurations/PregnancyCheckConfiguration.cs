using Hato.Modules.Breeding.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hato.Modules.Breeding.Infrastructure.Persistence.Configurations;

public class PregnancyCheckConfiguration : IEntityTypeConfiguration<PregnancyCheck>
{
    public void Configure(EntityTypeBuilder<PregnancyCheck> builder)
    {
        builder.ToTable("pregnancy_checks");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Method)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(c => c.Result)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(c => c.CheckedBy).HasMaxLength(100);
        builder.Property(c => c.Notes).HasMaxLength(500);

        builder.HasIndex(c => c.ServiceId);
        builder.HasIndex(c => c.DamId);
        builder.HasQueryFilter(c => c.DeletedAt == null);
    }
}

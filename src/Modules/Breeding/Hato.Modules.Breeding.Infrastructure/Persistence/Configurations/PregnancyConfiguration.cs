using Hato.Modules.Breeding.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hato.Modules.Breeding.Infrastructure.Persistence.Configurations;

public class PregnancyConfiguration : IEntityTypeConfiguration<Pregnancy>
{
    public void Configure(EntityTypeBuilder<Pregnancy> builder)
    {
        builder.ToTable("pregnancies");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(p => p.Notes).HasMaxLength(500);

        builder.HasIndex(p => p.DamId);
        builder.HasIndex(p => p.ExpectedBirthDate);
        builder.HasQueryFilter(p => p.DeletedAt == null);
    }
}

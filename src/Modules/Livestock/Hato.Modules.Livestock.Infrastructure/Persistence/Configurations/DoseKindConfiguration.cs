using Hato.Modules.Livestock.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hato.Modules.Livestock.Infrastructure.Persistence.Configurations;

public class DoseKindConfiguration : IEntityTypeConfiguration<DoseKind>
{
    public void Configure(EntityTypeBuilder<DoseKind> builder)
    {
        builder.ToTable("dose_kinds");
        builder.HasKey(k => k.Id);

        builder.Property(k => k.Key).HasMaxLength(50).IsRequired();
        builder.Property(k => k.LabelEs).HasMaxLength(100).IsRequired();
        builder.Property(k => k.IsActive).IsRequired();

        builder.HasIndex(k => k.Key).IsUnique().HasFilter("deleted_at IS NULL");
        builder.HasIndex(k => k.IsActive);
    }
}

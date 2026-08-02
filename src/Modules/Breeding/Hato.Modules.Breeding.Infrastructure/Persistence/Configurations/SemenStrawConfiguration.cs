using Hato.Modules.Breeding.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hato.Modules.Breeding.Infrastructure.Persistence.Configurations;

public class SemenStrawConfiguration : IEntityTypeConfiguration<SemenStraw>
{
    public void Configure(EntityTypeBuilder<SemenStraw> builder)
    {
        builder.ToTable("semen_straws");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Code).HasMaxLength(50).IsRequired();
        builder.Property(s => s.BullName).HasMaxLength(100).IsRequired();
        builder.Property(s => s.BullCode).HasMaxLength(50);
        builder.Property(s => s.SupplierName).HasMaxLength(100);
        builder.Property(s => s.Notes).HasMaxLength(500);

        builder.HasIndex(s => s.Code).IsUnique();
        builder.HasQueryFilter(s => s.DeletedAt == null);
    }
}

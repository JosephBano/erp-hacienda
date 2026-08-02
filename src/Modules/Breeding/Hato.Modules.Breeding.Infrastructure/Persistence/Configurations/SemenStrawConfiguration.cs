using Hato.Modules.Breeding.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hato.Modules.Breeding.Infrastructure.Persistence.Configurations;

public class SemenStrawConfiguration : IEntityTypeConfiguration<SemenStraw>
{
    public void Configure(EntityTypeBuilder<SemenStraw> builder)
    {
        builder.ToTable("semen_straws", t =>
        {
            t.HasCheckConstraint("CK_SemenStraw_CurrentQuantityNotNegative", "current_quantity >= 0");
        });
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Code).HasMaxLength(50).IsRequired();
        builder.Property(s => s.BullName).HasMaxLength(100).IsRequired();
        builder.Property(s => s.BullCode).HasMaxLength(50);
        builder.Property(s => s.SupplierName).HasMaxLength(100);
        builder.Property(s => s.Notes).HasMaxLength(500);

        builder.HasIndex(s => s.Code).IsUnique();
        builder.HasQueryFilter(s => s.DeletedAt == null);

        // Two concurrent inseminations against the last remaining straw must not both
        // succeed: xmin (Postgres system column) as an optimistic concurrency token makes
        // the second SaveChangesAsync throw DbUpdateConcurrencyException instead of
        // silently overwriting the first decrement.
        builder.Property<uint>("xmin").IsRowVersion();
    }
}

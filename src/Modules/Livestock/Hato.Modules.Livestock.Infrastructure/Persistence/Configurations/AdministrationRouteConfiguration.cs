using Hato.Modules.Livestock.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hato.Modules.Livestock.Infrastructure.Persistence.Configurations;

public class AdministrationRouteConfiguration : IEntityTypeConfiguration<AdministrationRoute>
{
    public void Configure(EntityTypeBuilder<AdministrationRoute> builder)
    {
        builder.ToTable("administration_routes");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Key).HasMaxLength(50).IsRequired();
        builder.Property(r => r.LabelEs).HasMaxLength(100).IsRequired();
        builder.Property(r => r.IsActive).IsRequired();
        builder.Property(r => r.DefaultUnitId).IsRequired(false);

        // Key is the wire-format identifier; it must be unique across all
        // active rows. The DB-level filter on deleted_at keeps the constraint
        // meaningful for soft-deleted rows that no longer appear in the
        // field-app's drop-down but still exist for historical references.
        builder.HasIndex(r => r.Key).IsUnique().HasFilter("deleted_at IS NULL");
        builder.HasIndex(r => r.IsActive);
    }
}
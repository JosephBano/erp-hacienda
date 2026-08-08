using Hato.Modules.Livestock.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hato.Modules.Livestock.Infrastructure.Persistence.Configurations;

/// <summary>
/// Persistence mapping for <see cref="PlausibilityRange"/> (ADR-0022).
/// The unique constraint (species_id, category_id, magnitude) is filtered by
/// <c>deleted_at IS NULL</c> so a deactivated row can be re-created with the
/// same combination after the deletion is reversed — same pattern as
/// <see cref="AdministrationRoute"/>.
/// </summary>
public class PlausibilityRangeConfiguration : IEntityTypeConfiguration<PlausibilityRange>
{
    public void Configure(EntityTypeBuilder<PlausibilityRange> builder)
    {
        builder.ToTable("plausibility_ranges");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.SpeciesId).IsRequired();
        builder.Property(r => r.CategoryId).IsRequired(false);
        builder.Property(r => r.Magnitude).HasMaxLength(50).IsRequired();
        builder.Property(r => r.PlausibleMin).HasColumnType("numeric(12,3)").IsRequired(false);
        builder.Property(r => r.PlausibleMax).HasColumnType("numeric(12,3)").IsRequired(false);
        builder.Property(r => r.AbsoluteMin).HasColumnType("numeric(12,3)").IsRequired(false);
        builder.Property(r => r.AbsoluteMax).HasColumnType("numeric(12,3)").IsRequired(false);
        builder.Property(r => r.IsActive).IsRequired();

        builder.HasOne<Species>().WithMany().HasForeignKey(r => r.SpeciesId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AnimalCategory>().WithMany().HasForeignKey(r => r.CategoryId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.SpeciesId, r.CategoryId, r.Magnitude })
            .IsUnique()
            .HasFilter("deleted_at IS NULL");
        builder.HasIndex(r => r.IsActive);
    }
}

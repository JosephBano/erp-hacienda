using Hato.Modules.Livestock.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hato.Modules.Livestock.Infrastructure.Persistence.Configurations;

public class HealthPlanConfiguration : IEntityTypeConfiguration<HealthPlan>
{
    public void Configure(EntityTypeBuilder<HealthPlan> builder)
    {
        builder.ToTable("health_plans");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(120).IsRequired();
        builder.Property(p => p.IsActive).IsRequired();

        builder.HasOne<Species>().WithMany().HasForeignKey(p => p.SpeciesId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.Name, p.SpeciesId })
            .IsUnique()
            .HasDatabaseName("i_x_health_plans_name_species_id")
            .HasFilter("deleted_at IS NULL");

        builder.Metadata.FindNavigation(nameof(HealthPlan.Items))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

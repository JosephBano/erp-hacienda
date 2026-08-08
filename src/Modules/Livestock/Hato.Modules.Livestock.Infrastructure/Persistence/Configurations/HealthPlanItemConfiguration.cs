using Hato.Modules.Livestock.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hato.Modules.Livestock.Infrastructure.Persistence.Configurations;

public class HealthPlanItemConfiguration : IEntityTypeConfiguration<HealthPlanItem>
{
    public void Configure(EntityTypeBuilder<HealthPlanItem> builder)
    {
        builder.ToTable("health_plan_items", t =>
        {
            t.HasCheckConstraint(
                "CK_HealthPlanItem_OffsetOrRepetition",
                "anchor_offset_days <> 0 OR repetitions IS NOT NULL");
        });

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Name).HasMaxLength(120).IsRequired();
        builder.Property(i => i.EventType).HasMaxLength(60).IsRequired();
        builder.Property(i => i.Anchor).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(i => i.AnchorOffsetDays).IsRequired();
        builder.Property(i => i.ComplianceWindowDays).IsRequired();
        builder.Property(i => i.IsActive).IsRequired();

        builder.Property(i => i.DoseQuantity).HasColumnType("numeric(12,3)");
        builder.Property(i => i.AppliesToSex).HasMaxLength(1).IsRequired(false);

        builder.HasOne<HealthPlan>().WithMany(p => p.Items).HasForeignKey(i => i.HealthPlanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AdministrationRoute>().WithMany().HasForeignKey(i => i.RouteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AnimalCategory>().WithMany().HasForeignKey(i => i.AppliesToCategoryId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.HealthPlanId).HasDatabaseName("i_x_health_plan_items_health_plan_id");
        builder.HasIndex(i => i.EventType).HasDatabaseName("i_x_health_plan_items_event_type");
        builder.HasIndex(i => i.IsActive).HasDatabaseName("i_x_health_plan_items_is_active");
    }
}

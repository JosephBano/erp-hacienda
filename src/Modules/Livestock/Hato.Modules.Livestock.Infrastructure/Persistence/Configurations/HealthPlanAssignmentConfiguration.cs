using Hato.Modules.Livestock.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hato.Modules.Livestock.Infrastructure.Persistence.Configurations;

public class HealthPlanAssignmentConfiguration : IEntityTypeConfiguration<HealthPlanAssignment>
{
    public void Configure(EntityTypeBuilder<HealthPlanAssignment> builder)
    {
        builder.ToTable("health_plan_assignments", t =>
        {
            t.HasCheckConstraint(
                "CK_HealthPlanAssignment_AnimalXorGroup",
                "(animal_id IS NOT NULL) <> (group_id IS NOT NULL)");
        });

        builder.HasKey(a => a.Id);

        builder.Property(a => a.AssignedAt).IsRequired();
        builder.Property(a => a.IsActive).IsRequired();

        builder.HasOne<HealthPlan>().WithMany().HasForeignKey(a => a.HealthPlanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Animal>().WithMany().HasForeignKey(a => a.AnimalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AnimalGroup>().WithMany().HasForeignKey(a => a.GroupId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.HealthPlanId).HasDatabaseName("i_x_health_plan_assignments_health_plan_id");
        builder.HasIndex(a => a.AnimalId).HasDatabaseName("i_x_health_plan_assignments_animal_id");
        builder.HasIndex(a => a.GroupId).HasDatabaseName("i_x_health_plan_assignments_group_id");
        builder.HasIndex(a => a.IsActive).HasDatabaseName("i_x_health_plan_assignments_is_active");
    }
}

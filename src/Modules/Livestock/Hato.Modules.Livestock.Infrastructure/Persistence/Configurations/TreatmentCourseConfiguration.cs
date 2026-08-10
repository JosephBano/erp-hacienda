using Hato.Modules.Livestock.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hato.Modules.Livestock.Infrastructure.Persistence.Configurations;

public class TreatmentCourseConfiguration : IEntityTypeConfiguration<TreatmentCourse>
{
    public void Configure(EntityTypeBuilder<TreatmentCourse> builder)
    {
        builder.ToTable("treatment_courses", t =>
        {
            // Same XOR discipline as animal_events (ADR-0015 sec.2): a course is
            // about exactly one subject.
            t.HasCheckConstraint(
                "CK_TreatmentCourse_AnimalXorGroup",
                "(animal_id IS NOT NULL) <> (group_id IS NOT NULL)");
        });

        builder.HasKey(c => c.Id);

        builder.Property(c => c.AnimalId).IsRequired(false);
        builder.Property(c => c.GroupId).IsRequired(false);
        builder.Property(c => c.StartsAt).IsRequired();
        builder.Property(c => c.EndsAt).IsRequired(false);
        builder.Property(c => c.Reason).HasMaxLength(50).IsRequired(false);
        builder.Property(c => c.ProductId).IsRequired(false);
        builder.Property(c => c.DoseFactorAmount).HasColumnType("numeric(12,3)").IsRequired();
        builder.Property(c => c.DoseFactorUnit).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Notes).HasColumnType("text").IsRequired(false);
        builder.Property(c => c.IsSynthetic).IsRequired();

        builder.HasOne<Animal>().WithMany().HasForeignKey(c => c.AnimalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AnimalGroup>().WithMany().HasForeignKey(c => c.GroupId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AdministrationRoute>().WithMany().HasForeignKey(c => c.RouteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DoseKind>().WithMany().HasForeignKey(c => c.DoseKindId).OnDelete(DeleteBehavior.Restrict);
        // ProductId: soft FK, see the property doc comment — inventory does not
        // share a schema with livestock (Art. 6).

        builder.HasMany(c => c.Applications)
            .WithOne()
            .HasForeignKey(a => a.TreatmentCourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(TreatmentCourse.Applications))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(c => new { c.AnimalId, c.StartsAt });
        builder.HasIndex(c => new { c.GroupId, c.StartsAt });
        builder.HasIndex(c => c.RouteId);
        builder.HasIndex(c => c.DoseKindId);
    }
}

public class TreatmentCourseApplicationConfiguration : IEntityTypeConfiguration<TreatmentCourseApplication>
{
    public void Configure(EntityTypeBuilder<TreatmentCourseApplication> builder)
    {
        builder.ToTable("treatment_course_applications", t =>
        {
            t.HasCheckConstraint(
                "CK_TreatmentCourseApplication_ApplicationNoPositive",
                "application_no > 0");
        });

        builder.HasKey(a => a.Id);

        builder.Property(a => a.TreatmentCourseId).IsRequired();
        builder.Property(a => a.ApplicationNo).IsRequired();
        builder.Property(a => a.AppliedAt).IsRequired();

        builder.Property(a => a.CalculatedDoseAmount).HasColumnType("numeric(12,3)").IsRequired(false);
        builder.Property(a => a.CalculatedDoseUnit).HasMaxLength(20).IsRequired(false);
        builder.Property(a => a.IsEstimated).IsRequired();

        builder.Property(a => a.AdministeredDoseAmount).HasColumnType("numeric(12,3)").IsRequired(false);
        builder.Property(a => a.AdministeredDoseUnit).HasMaxLength(20).IsRequired(false);

        builder.Property(a => a.Notes).HasColumnType("text").IsRequired(false);
        builder.Property(a => a.IsPlausibilityConfirmed).IsRequired().HasDefaultValue(false);

        builder.HasIndex(a => new { a.TreatmentCourseId, a.ApplicationNo }).IsUnique();
        builder.HasIndex(a => a.AppliedAt);
    }
}

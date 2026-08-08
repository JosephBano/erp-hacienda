using Hato.Modules.Breeding.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hato.Modules.Breeding.Infrastructure.Persistence.Configurations;

public class NursingCohortConfiguration : IEntityTypeConfiguration<NursingCohort>
{
    public void Configure(EntityTypeBuilder<NursingCohort> builder)
    {
        builder.ToTable("nursing_cohorts");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.SpeciesId).IsRequired();
        builder.Property(c => c.StartedAt).IsRequired();
        builder.Property(c => c.ClosedAt);
        builder.Property(c => c.WeanedAt);
        builder.Property(c => c.SortedAt);
        builder.Property(c => c.Notes).HasMaxLength(500);

        // Find the open cohort for a species when assigning a new birthing.
        builder.HasIndex(c => new { c.SpeciesId, c.ClosedAt });

        // Date-window queries ("which cohorts were open around this date").
        builder.HasIndex(c => c.StartedAt);

        builder.HasQueryFilter(c => c.DeletedAt == null);
    }
}

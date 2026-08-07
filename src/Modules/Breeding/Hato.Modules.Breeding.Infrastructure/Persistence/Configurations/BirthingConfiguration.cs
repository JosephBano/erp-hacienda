using Hato.Modules.Breeding.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hato.Modules.Breeding.Infrastructure.Persistence.Configurations;

public class BirthingConfiguration : IEntityTypeConfiguration<Birthing>
{
    public void Configure(EntityTypeBuilder<Birthing> builder)
    {
        builder.ToTable("birthings");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Difficulty)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(b => b.LitterWeight).HasPrecision(6, 2);
        builder.Property(b => b.Notes).HasMaxLength(500);

        builder.HasIndex(b => b.DamId);
        builder.HasIndex(b => b.BirthDate);
        builder.HasIndex(b => b.NursingCohortId);

        builder.HasOne<NursingCohort>()
            .WithMany()
            .HasForeignKey(b => b.NursingCohortId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(b => b.DeletedAt == null);
    }
}

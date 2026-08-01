using Hato.Modules.Livestock.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hato.Modules.Livestock.Infrastructure.Persistence.Configurations;

public class AnimalEventConfiguration : IEntityTypeConfiguration<AnimalEvent>
{
    public void Configure(EntityTypeBuilder<AnimalEvent> builder)
    {
        builder.ToTable("animal_events");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.EventType).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.RecordedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.PayloadJson).HasColumnType("jsonb").IsRequired();

        builder.HasOne<Animal>().WithMany().HasForeignKey(e => e.AnimalId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.AnimalId, e.OccurredAt });
    }
}

public class WithdrawalPeriodConfiguration : IEntityTypeConfiguration<WithdrawalPeriod>
{
    public void Configure(EntityTypeBuilder<WithdrawalPeriod> builder)
    {
        builder.ToTable("withdrawal_periods");
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Target).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasOne<Animal>().WithMany().HasForeignKey(w => w.AnimalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AnimalEvent>().WithMany().HasForeignKey(w => w.EventId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(w => new { w.AnimalId, w.EndsAt });
    }
}

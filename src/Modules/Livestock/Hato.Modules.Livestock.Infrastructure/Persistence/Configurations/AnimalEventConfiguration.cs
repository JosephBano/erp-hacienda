using Hato.Modules.Livestock.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hato.Modules.Livestock.Infrastructure.Persistence.Configurations;

public class AnimalEventConfiguration : IEntityTypeConfiguration<AnimalEvent>
{
    public void Configure(EntityTypeBuilder<AnimalEvent> builder)
    {
        builder.ToTable("animal_events", t =>
        {
            // Belt-and-suspenders on top of the domain (ADR-0015 sec.2): a consumer that
            // still assumes AnimalId is never null fails loudly writing to the database,
            // not silently reading a group event as if it had no subject.
            t.HasCheckConstraint(
                "CK_AnimalEvent_AnimalXorGroup",
                "(animal_id IS NOT NULL) <> (group_id IS NOT NULL)");

            t.HasCheckConstraint(
                "CK_AnimalEvent_AffectedCountPositive",
                "affected_count IS NULL OR affected_count > 0");
        });
        builder.HasKey(e => e.Id);

        builder.Property(e => e.EventType).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.RecordedByLabel).HasMaxLength(100).IsRequired();
        builder.Property(e => e.RecordedById).IsRequired(false);
        builder.Ignore(e => e.RecordedBy);

        builder.Property(e => e.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.AnimalId).IsRequired(false);
        builder.Property(e => e.GroupId).IsRequired(false);
        builder.Property(e => e.AffectedCount).IsRequired(false);
        builder.Property(e => e.CauseId).IsRequired(false);
        builder.Property(e => e.RouteId).IsRequired(false);
        builder.Property(e => e.Reason).HasMaxLength(50).IsRequired(false);
        builder.Property(e => e.BatchId).IsRequired(false);
        builder.Property(e => e.HealthPlanItemId).IsRequired(false);
        builder.Property(e => e.AppliedByUserId).IsRequired(false);

        builder.HasOne<Animal>().WithMany().HasForeignKey(e => e.AnimalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AnimalGroup>().WithMany().HasForeignKey(e => e.GroupId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MortalityCause>().WithMany().HasForeignKey(e => e.CauseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AdministrationRoute>().WithMany().HasForeignKey(e => e.RouteId).OnDelete(DeleteBehavior.Restrict);
        // FK cross-schema a inventory_batches y a people.users son soft FKs:
        // el catálogo de inventory no comparte esquema con livestock y la tabla
        // users vive en people. Las FKs estrictas llegan en Fase 4 cuando la
        // arquitectura decida cómo conectar módulos. Por ahora, índices para
        // que las consultas por lote / por usuario sigan siendo rápidas.
        // FK a health_plan_items llegará con 3.5b.1 (ADR-0016) — por ahora la
        // columna existe como nullable y sin FK.

        builder.HasIndex(e => new { e.AnimalId, e.OccurredAt });
        builder.HasIndex(e => new { e.GroupId, e.OccurredAt });
        builder.HasIndex(e => e.RecordedById);
        builder.HasIndex(e => e.RouteId);
        builder.HasIndex(e => e.Reason);
        builder.HasIndex(e => e.BatchId);
        builder.HasIndex(e => e.AppliedByUserId);
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

using Hato.Modules.Inventory.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hato.Modules.Inventory.Infrastructure.Persistence.Configurations;

public class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.ToTable("inventory_items");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Name).HasMaxLength(100).IsRequired();
        builder.Property(i => i.Category).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(i => i.Unit).HasMaxLength(20).IsRequired();
        builder.Property(i => i.Description).HasMaxLength(500);
        builder.Property(i => i.FeedStageId).IsRequired(false);

        // Restrict, not cascade: deactivating/removing a feed stage must never silently
        // wipe the classification off items that reference it (Art. 1).
        builder.HasOne<FeedStage>().WithMany().HasForeignKey(i => i.FeedStageId).OnDelete(DeleteBehavior.Restrict);

        builder.Metadata.FindNavigation(nameof(InventoryItem.Batches))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class FeedStageConfiguration : IEntityTypeConfiguration<FeedStage>
{
    public void Configure(EntityTypeBuilder<FeedStage> builder)
    {
        builder.ToTable("feed_stages");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Key).HasMaxLength(50).IsRequired();
        builder.Property(s => s.LabelEs).HasMaxLength(100).IsRequired();
        builder.Property(s => s.IsActive).IsRequired();

        // Key is the wire-format identifier; unique across active rows only, same pattern
        // as AdministrationRoute/TreatmentReason — a deactivated row keeps its old key so
        // historical InventoryItem.FeedStageId references stay meaningful.
        builder.HasIndex(s => s.Key).IsUnique().HasFilter("deleted_at IS NULL");
        builder.HasIndex(s => s.IsActive);
    }
}

public class InventoryBatchConfiguration : IEntityTypeConfiguration<InventoryBatch>
{
    public void Configure(EntityTypeBuilder<InventoryBatch> builder)
    {
        builder.ToTable("inventory_batches");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.BatchNumber).HasMaxLength(50).IsRequired();
        builder.Property(b => b.ReceivedAt).IsRequired();
        builder.Property(b => b.SupplierLabel).HasMaxLength(200);
        builder.Property(b => b.InvoiceReference).HasMaxLength(100);
        builder.Property(b => b.Notes).HasMaxLength(500);
        builder.Property(b => b.RecordedByLabel).HasMaxLength(200);
        // RecordedById stays a soft FK (no REFERENCES) — same orphan pattern as
        // GroupFeedConsumption.RecordedById (ADR-0026 Decisión 1).
        builder.HasIndex(b => b.ReceivedAt).HasDatabaseName("ix_inventory_batches_received_at");
        builder.HasOne<InventoryItem>().WithMany(i => i.Batches).HasForeignKey(b => b.InventoryItemId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class GroupFeedConsumptionConfiguration : IEntityTypeConfiguration<GroupFeedConsumption>
{
    public void Configure(EntityTypeBuilder<GroupFeedConsumption> builder)
    {
        builder.ToTable("group_feed_consumptions");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.RecordedByLabel).HasMaxLength(100).IsRequired();
        builder.Property(c => c.RecordedById).IsRequired(false);

        // The unit the operator typed lives under the same rules as InventoryItem.Unit —
        // it is the same vocabulary. The factor shares the precision of
        // unit_conversions.factor, which is where it came from (Art. 10: quantities carry
        // their unit, and the conversion that produced them is part of the record).
        builder.Property(c => c.UnitRecorded).HasMaxLength(20).IsRequired();
        builder.Property(c => c.QuantityRecorded).HasColumnType("numeric(18,3)");
        builder.Property(c => c.QuantityInBaseUnit).HasColumnType("numeric(18,3)");
        builder.Property(c => c.AppliedFactor).HasColumnType("numeric(18,6)");
        builder.Ignore(c => c.RecordedBy);
        builder.Property(c => c.Notes).HasMaxLength(500);

        builder.HasOne<InventoryItem>().WithMany().HasForeignKey(c => c.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => new { c.GroupId, c.ConsumedAt });
        builder.HasIndex(c => c.RecordedById);
    }
}

public class UnitConversionConfiguration : IEntityTypeConfiguration<UnitConversion>
{
    public void Configure(EntityTypeBuilder<UnitConversion> builder)
    {
        builder.ToTable("unit_conversions");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.FromUnit).HasMaxLength(20).IsRequired();
        builder.Property(u => u.ToUnit).HasMaxLength(20).IsRequired();
        builder.Property(u => u.Factor).HasColumnType("numeric(18,6)").IsRequired();

        builder.HasOne<InventoryItem>().WithMany().HasForeignKey(u => u.InventoryItemId).OnDelete(DeleteBehavior.Cascade);
        // (item, from, to) is unique: a single explicit conversion per direction.
        builder.HasIndex(u => new { u.InventoryItemId, u.FromUnit, u.ToUnit }).IsUnique();
    }
}

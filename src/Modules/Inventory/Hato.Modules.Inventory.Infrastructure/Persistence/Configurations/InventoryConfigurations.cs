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

        builder.Metadata.FindNavigation(nameof(InventoryItem.Batches))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class InventoryBatchConfiguration : IEntityTypeConfiguration<InventoryBatch>
{
    public void Configure(EntityTypeBuilder<InventoryBatch> builder)
    {
        builder.ToTable("inventory_batches");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.BatchNumber).HasMaxLength(50).IsRequired();
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

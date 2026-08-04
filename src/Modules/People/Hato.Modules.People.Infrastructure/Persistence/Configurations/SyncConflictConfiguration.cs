using Hato.Modules.People.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hato.Modules.People.Infrastructure.Persistence.Configurations;

public class SyncConflictConfiguration : IEntityTypeConfiguration<SyncConflict>
{
    public void Configure(EntityTypeBuilder<SyncConflict> builder)
    {
        builder.ToTable("sync_conflicts", PeopleDbContext.Schema);

        builder.HasKey(c => c.Id);

        builder.Property(c => c.EntityType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.EntityId)
            .IsRequired();

        builder.HasIndex(c => new { c.EntityType, c.EntityId });

        builder.Property(c => c.FieldName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.ServerValue)
            .HasColumnType("text");

        builder.Property(c => c.AttemptedValue)
            .HasColumnType("text");

        builder.Property(c => c.Resolution)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.DeviceId)
            .HasMaxLength(100);

        builder.Property(c => c.DetectedAt)
            .IsRequired();
    }
}

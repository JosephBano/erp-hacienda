using Hato.Modules.People.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hato.Modules.People.Infrastructure.Persistence.Configurations;

public class SyncOperationConfiguration : IEntityTypeConfiguration<SyncOperation>
{
    public void Configure(EntityTypeBuilder<SyncOperation> builder)
    {
        builder.ToTable("sync_operations", PeopleDbContext.Schema);

        builder.HasKey(s => s.Id);

        builder.Property(s => s.ClientOperationId)
            .IsRequired();

        builder.HasIndex(s => s.ClientOperationId)
            .IsUnique();

        builder.Property(s => s.UserId)
            .IsRequired();

        builder.Property(s => s.DeviceId)
            .HasMaxLength(100);

        builder.Property(s => s.OperationType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.PayloadJson)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(s => s.OccurredAt)
            .IsRequired();

        builder.Property(s => s.ReceivedAt)
            .IsRequired();

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.ResultRef)
            .HasMaxLength(255);

        builder.Property(s => s.ErrorDetails)
            .HasColumnType("text");
    }
}

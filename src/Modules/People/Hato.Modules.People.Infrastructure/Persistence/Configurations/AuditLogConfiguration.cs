using Hato.Modules.People.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hato.Modules.People.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Action).HasMaxLength(50).IsRequired();
        builder.Property(a => a.Module).HasMaxLength(50).IsRequired();
        builder.Property(a => a.EntityName).HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityId).HasMaxLength(100).IsRequired();
        builder.Property(a => a.UserEmail).HasMaxLength(150);
        builder.Property(a => a.UserFullName).HasMaxLength(100);

        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => a.Timestamp);
        builder.HasIndex(a => new { a.UserId, a.Timestamp });
    }
}

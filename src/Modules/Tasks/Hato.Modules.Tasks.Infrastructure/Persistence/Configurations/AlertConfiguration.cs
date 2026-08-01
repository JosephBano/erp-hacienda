using Hato.Modules.Tasks.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hato.Modules.Tasks.Infrastructure.Persistence.Configurations;

public class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.ToTable("alerts");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Code).HasMaxLength(50).IsRequired();
        builder.Property(a => a.Title).HasMaxLength(150).IsRequired();
        builder.Property(a => a.Message).HasMaxLength(500).IsRequired();

        builder.Property(a => a.Severity)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(a => a.IsDismissed);
        builder.HasIndex(a => a.Code);
        builder.HasQueryFilter(a => a.DeletedAt == null);
    }
}

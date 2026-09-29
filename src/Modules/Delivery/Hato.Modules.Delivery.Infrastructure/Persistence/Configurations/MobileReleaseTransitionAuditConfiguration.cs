using Hato.Modules.Delivery.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hato.Modules.Delivery.Infrastructure.Persistence.Configurations;

public class MobileReleaseTransitionAuditConfiguration : IEntityTypeConfiguration<MobileReleaseTransitionAudit>
{
    public void Configure(EntityTypeBuilder<MobileReleaseTransitionAudit> builder)
    {
        builder.ToTable("mobile_release_transition_audits");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.FromStatus)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.ToStatus)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasMaxLength(500);

        builder.HasIndex(x => x.ReleaseId);
        builder.HasIndex(x => x.ChangedAt);
    }
}

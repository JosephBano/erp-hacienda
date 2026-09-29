using Hato.Modules.Delivery.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hato.Modules.Delivery.Infrastructure.Persistence.Configurations;

public class MobileReleaseConfiguration : IEntityTypeConfiguration<MobileRelease>
{
    public void Configure(EntityTypeBuilder<MobileRelease> builder)
    {
        builder.ToTable("mobile_releases");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Channel)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Version)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.PackageName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.TargetApiUrl)
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(x => x.CommitSha)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(x => x.ReleaseTag)
            .HasMaxLength(50);

        builder.Property(x => x.ArtifactFileName)
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(x => x.ArtifactSha256)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.SigningCertificateFingerprint)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasMaxLength(1000);

        builder.Property(x => x.ApiCompatibility)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.WithdrawReason)
            .HasMaxLength(500);

        builder.HasMany(x => x.TransitionAudits)
            .WithOne()
            .HasForeignKey(x => x.ReleaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.PackageName, x.VersionCode })
            .IsUnique();

        builder.HasIndex(x => x.Channel)
            .IsUnique()
            .HasFilter("is_current_stable = true");

        builder.HasIndex(x => new { x.Channel, x.Status });
        builder.HasIndex(x => x.CreatedAt);
    }
}

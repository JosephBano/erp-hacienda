using Hato.Modules.Delivery.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hato.Modules.Delivery.Infrastructure.Persistence.Configurations;

public class MobileBuildRequestConfiguration : IEntityTypeConfiguration<MobileBuildRequest>
{
    public void Configure(EntityTypeBuilder<MobileBuildRequest> builder)
    {
        builder.ToTable("mobile_build_requests");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Channel)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.CommitSha)
            .HasMaxLength(40)
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

        builder.Property(x => x.IdempotencyKey)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.ReleaseTag)
            .HasMaxLength(50);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(1000);

        builder.HasIndex(x => new { x.Channel, x.IdempotencyKey })
            .IsUnique();

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CreatedAt);
    }
}

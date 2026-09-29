using Hato.Modules.Delivery.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hato.Modules.Delivery.Infrastructure.Persistence.Configurations;

public class MobileVersionCodeSequenceConfiguration : IEntityTypeConfiguration<MobileVersionCodeSequence>
{
    public void Configure(EntityTypeBuilder<MobileVersionCodeSequence> builder)
    {
        builder.ToTable("mobile_version_code_sequences");

        builder.HasKey(x => x.PackageName);

        builder.Property(x => x.PackageName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.LastVersionCode)
            .IsRequired();
    }
}

using Hato.Modules.Livestock.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hato.Modules.Livestock.Infrastructure.Persistence.Configurations;

public class TreatmentReasonConfiguration : IEntityTypeConfiguration<TreatmentReason>
{
    public void Configure(EntityTypeBuilder<TreatmentReason> builder)
    {
        builder.ToTable("treatment_reasons");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Key).HasMaxLength(50).IsRequired();
        builder.Property(r => r.LabelEs).HasMaxLength(100).IsRequired();
        builder.Property(r => r.IsActive).IsRequired();

        builder.HasIndex(r => r.Key).IsUnique().HasFilter("deleted_at IS NULL");
        builder.HasIndex(r => r.IsActive);
    }
}
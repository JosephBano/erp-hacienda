using Hato.Modules.Livestock.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hato.Modules.Livestock.Infrastructure.Persistence.Configurations;

public class MortalityCauseConfiguration : IEntityTypeConfiguration<MortalityCause>
{
    public void Configure(EntityTypeBuilder<MortalityCause> builder)
    {
        builder.ToTable("mortality_causes");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.Property(c => c.IsActive).IsRequired();

        builder.HasIndex(c => c.Name).IsUnique();
    }
}

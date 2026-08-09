using Hato.Modules.People.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hato.Modules.People.Infrastructure.Persistence.Configurations;

public class FarmModuleConfiguration : IEntityTypeConfiguration<FarmModule>
{
    public void Configure(EntityTypeBuilder<FarmModule> builder)
    {
        builder.ToTable("farm_modules");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Key).HasMaxLength(50).IsRequired();
        builder.Property(m => m.DisabledReason).HasMaxLength(500);

        // The whole module toggle list is small. The index keeps the lookup O(1)
        // even after several toggles flip enabled/disabled back and forth.
        builder.HasIndex(m => m.Key).IsUnique();
    }
}

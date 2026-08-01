using Hato.Modules.Livestock.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hato.Modules.Livestock.Infrastructure.Persistence.Configurations;

public class AnimalCategoryConfiguration : IEntityTypeConfiguration<AnimalCategory>
{
    public void Configure(EntityTypeBuilder<AnimalCategory> builder)
    {
        builder.ToTable("animal_categories");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();

        builder.HasOne<Species>()
            .WithMany()
            .HasForeignKey(c => c.SpeciesId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

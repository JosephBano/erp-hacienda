using Hato.Modules.Livestock.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hato.Modules.Livestock.Infrastructure.Persistence.Configurations;

public class AnimalGroupConfiguration : IEntityTypeConfiguration<AnimalGroup>
{
    public void Configure(EntityTypeBuilder<AnimalGroup> builder)
    {
        builder.ToTable("animal_groups");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Name).HasMaxLength(100).IsRequired();
        builder.Property(g => g.Description).HasMaxLength(500);
        builder.Property(g => g.TrackingMode).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasOne<Species>().WithMany().HasForeignKey(g => g.SpeciesId).OnDelete(DeleteBehavior.Restrict);

        builder.Metadata.FindNavigation(nameof(AnimalGroup.Memberships))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class GroupMembershipConfiguration : IEntityTypeConfiguration<GroupMembership>
{
    public void Configure(EntityTypeBuilder<GroupMembership> builder)
    {
        builder.ToTable("group_memberships");
        builder.HasKey(m => m.Id);

        builder.HasOne<AnimalGroup>().WithMany(g => g.Memberships).HasForeignKey(m => m.GroupId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Animal>().WithMany().HasForeignKey(m => m.AnimalId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new { m.GroupId, m.AnimalId, m.JoinedAt });
    }
}

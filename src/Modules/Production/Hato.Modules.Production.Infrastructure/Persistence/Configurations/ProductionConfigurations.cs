using Hato.Modules.Production.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hato.Modules.Production.Infrastructure.Persistence.Configurations;

public class MilkingSessionConfiguration : IEntityTypeConfiguration<MilkingSession>
{
    public void Configure(EntityTypeBuilder<MilkingSession> builder)
    {
        builder.ToTable("milking_sessions");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Shift).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(s => s.RecordedByLabel).HasMaxLength(100).IsRequired();
        builder.Property(s => s.RecordedById).IsRequired(false);
        builder.Ignore(s => s.RecordedBy);
        builder.Property(s => s.Notes).HasMaxLength(500);

        builder.Metadata.FindNavigation(nameof(MilkingSession.Yields))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(s => new { s.Date, s.Shift });
        builder.HasIndex(s => s.RecordedById);
    }
}

public class MilkYieldConfiguration : IEntityTypeConfiguration<MilkYield>
{
    public void Configure(EntityTypeBuilder<MilkYield> builder)
    {
        builder.ToTable("milk_yields");
        builder.HasKey(y => y.Id);

        builder.HasOne<MilkingSession>().WithMany(s => s.Yields).HasForeignKey(y => y.MilkingSessionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(y => y.AnimalId);
    }
}

public class LactationConfiguration : IEntityTypeConfiguration<Lactation>
{
    public void Configure(EntityTypeBuilder<Lactation> builder)
    {
        builder.ToTable("lactations");
        builder.HasKey(l => l.Id);

        builder.HasIndex(l => new { l.AnimalId, l.LactationNumber }).IsUnique();
    }
}

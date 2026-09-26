using KarraMatcher.Domain.Teams;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KarraMatcher.Infrastructure.Persistence.Configurations;

internal sealed class ClubConfiguration : IEntityTypeConfiguration<Club>
{
    public void Configure(EntityTypeBuilder<Club> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Slug).HasMaxLength(50).IsRequired();
        builder.HasIndex(c => c.Slug).IsUnique();

        // Klubbens hemmaplan (`#307`). Nullbara tills en tränare satt den.
        builder.Property(c => c.HomeVenueName).HasMaxLength(100);
        builder.Property(c => c.HomeAddress).HasMaxLength(200);

        builder.HasMany(c => c.AgeGroups)
            .WithOne(a => a.Club)
            .HasForeignKey(a => a.ClubId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

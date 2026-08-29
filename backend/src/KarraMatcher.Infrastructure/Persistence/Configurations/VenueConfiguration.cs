using KarraMatcher.Domain.Matches;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KarraMatcher.Infrastructure.Persistence.Configurations;

internal sealed class VenueConfiguration : IEntityTypeConfiguration<Venue>
{
    public void Configure(EntityTypeBuilder<Venue> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Name).HasMaxLength(100).IsRequired();
        builder.Property(v => v.Address).HasMaxLength(200).IsRequired();
        builder.HasIndex(v => v.Name).IsUnique();

        builder.HasMany(v => v.Matches)
            .WithOne(m => m.Venue)
            .HasForeignKey(m => m.VenueId)
            // En spelplats som används av matcher får inte raderas bort under fötterna.
            .OnDelete(DeleteBehavior.Restrict);
    }
}

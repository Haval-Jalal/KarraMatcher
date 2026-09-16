using KarraMatcher.Domain.Teams;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KarraMatcher.Infrastructure.Persistence.Configurations;

internal sealed class SportConfiguration : IEntityTypeConfiguration<Sport>
{
    public void Configure(EntityTypeBuilder<Sport> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).HasMaxLength(50).IsRequired();
        builder.Property(s => s.Slug).HasMaxLength(50).IsRequired();

        builder.HasIndex(s => s.Slug).IsUnique();

        // Relationen till trupperna konfigureras på AgeGroup-sidan (SportId, Restrict).
    }
}

using KarraMatcher.Domain.Teams;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KarraMatcher.Infrastructure.Persistence.Configurations;

internal sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(50).IsRequired();
        builder.Property(t => t.ColorHex).HasMaxLength(7).IsRequired();
        builder.Property(t => t.Slug).HasMaxLength(50).IsRequired();

        // Kallelsen levereras avstängd (§KM.7). Standardvärdet sätts i databasen så
        // att en rad som skapas utanför appen inte råkar slå på funktionen.
        builder.Property(t => t.AttendanceEnabled).HasDefaultValue(false).IsRequired();

        builder.HasIndex(t => t.Slug).IsUnique();
    }
}

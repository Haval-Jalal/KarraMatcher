using KarraMatcher.Domain.Teams;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KarraMatcher.Infrastructure.Persistence.Configurations;

internal sealed class AgeGroupConfiguration : IEntityTypeConfiguration<AgeGroup>
{
    public void Configure(EntityTypeBuilder<AgeGroup> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Name).HasMaxLength(50).IsRequired();
        builder.Property(a => a.Season).HasMaxLength(20).IsRequired();

        // En åldersgrupp per namn och säsong inom en förening.
        builder.HasIndex(a => new { a.ClubId, a.Name, a.Season }).IsUnique();

        // Sporten truppen tillhör (v2). Restrict: en sport som har trupper går inte att
        // radera av misstag och ta med sig hela årskullar i fallet.
        builder.HasOne(a => a.Sport)
            .WithMany(s => s.AgeGroups)
            .HasForeignKey(a => a.SportId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(a => a.Teams)
            .WithOne(t => t.AgeGroup)
            .HasForeignKey(t => t.AgeGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

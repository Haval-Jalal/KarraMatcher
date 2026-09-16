using KarraMatcher.Domain.Children;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KarraMatcher.Infrastructure.Persistence.Configurations;

internal sealed class GuardianshipConfiguration : IEntityTypeConfiguration<Guardianship>
{
    public void Configure(EntityTypeBuilder<Guardianship> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(g => g.Id);
        builder.Property(g => g.GrantedUtc).HasColumnType("timestamp with time zone");

        // Raderas kontot försvinner dess vårdnadshavarkopplingar (§KM.6).
        builder.HasOne(g => g.Account)
            .WithMany()
            .HasForeignKey(g => g.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        // Raderas barnet försvinner kopplingarna till det.
        builder.HasOne(g => g.Child)
            .WithMany()
            .HasForeignKey(g => g.ChildId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ett konto är vårdnadshavare för ett barn högst en gång.
        builder.HasIndex(g => new { g.AccountId, g.ChildId }).IsUnique();
    }
}

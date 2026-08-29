using KarraMatcher.Domain.Matches;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KarraMatcher.Infrastructure.Persistence.Configurations;

internal sealed class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(m => m.Id);

        // timestamptz, inte timestamp. Npgsql kräver då att DateTime.Kind är Utc och
        // kastar annars — vilket är precis den vakthund vi vill ha för §KM.5.
        builder.Property(m => m.KickoffUtc).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(m => m.UpdatedUtc).HasColumnType("timestamp with time zone").IsRequired();

        builder.Property(m => m.OpponentName).HasMaxLength(120).IsRequired();
        builder.Property(m => m.AddressOverride).HasMaxLength(200);
        builder.Property(m => m.Note).HasMaxLength(500);

        // Status lagras som text. En siffra i databasen säger ingenting den dag
        // någon felsöker med psql klockan sju en lördagmorgon.
        builder.Property(m => m.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(MatchStatus.Scheduled)
            .IsRequired();

        builder.Property(m => m.IcsSequence).HasDefaultValue(0).IsRequired();

        // Den vanligaste frågan i hela appen: ett lags matcher i tidsordning.
        builder.HasIndex(m => new { m.TeamId, m.KickoffUtc });

        builder.HasOne(m => m.Team)
            .WithMany(t => t.Matches)
            .HasForeignKey(m => m.TeamId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

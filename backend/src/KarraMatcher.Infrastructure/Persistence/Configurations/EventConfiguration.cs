using KarraMatcher.Domain.Events;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KarraMatcher.Infrastructure.Persistence.Configurations;

internal sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Events");

        builder.HasKey(e => e.Id);

        // timestamptz, inte timestamp. Npgsql kräver då att DateTime.Kind är Utc och
        // kastar annars — vilket är precis den vakthund vi vill ha för §KM.5.
        builder.Property(e => e.KickoffUtc).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(e => e.UpdatedUtc).HasColumnType("timestamp with time zone").IsRequired();

        // Idempotens-markören för kvällspåminnelsen (#64). Nullbar: null betyder "inte påmind än".
        builder.Property(e => e.ReminderSentUtc).HasColumnType("timestamp with time zone");

        // Typ lagras som text — en siffra säger ingenting vid felsökning med psql.
        // Befintliga rader migrerades till Match (`#198`).
        builder.Property(e => e.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(EventType.Match)
            .IsRequired();

        // Rubrik för träning/övrigt; motståndare/hemma-borta bara för en match — därför nullbara.
        builder.Property(e => e.Title).HasMaxLength(120);
        builder.Property(e => e.OpponentName).HasMaxLength(120);
        builder.Property(e => e.AddressOverride).HasMaxLength(200);
        builder.Property(e => e.Note).HasMaxLength(500);

        // Status lagras som text. En siffra i databasen säger ingenting den dag
        // någon felsöker med psql klockan sju en lördagmorgon.
        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(EventStatus.Scheduled)
            .IsRequired();

        builder.Property(e => e.IcsSequence).HasDefaultValue(0).IsRequired();

        // Den vanligaste frågan i hela appen: ett lags händelser i tidsordning.
        builder.HasIndex(e => new { e.TeamId, e.KickoffUtc });

        builder.HasOne(e => e.Team)
            .WithMany(t => t.Events)
            .HasForeignKey(e => e.TeamId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

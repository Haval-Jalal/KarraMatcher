using KarraMatcher.Domain.Attendance;
using KarraMatcher.Domain.Matches;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KarraMatcher.Infrastructure.Persistence.Configurations;

internal sealed class AttendanceCallConfiguration : IEntityTypeConfiguration<AttendanceCall>
{
    public void Configure(EntityTypeBuilder<AttendanceCall> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(c => c.Id);

        builder.Property(c => c.OpenedUtc).HasColumnType("timestamp with time zone").IsRequired();

        // En match kallas en gang. Oppnandet ar idempotent i handlern, men garantin ligger i
        // ett unikt index -- tva samtidiga anrop kan bada lasa "ingen kallelse finns".
        builder.HasIndex(c => c.MatchId).IsUnique();

        /*
         * Kaskad fran matchen: en kallelse till en match som inte finns ar inget.
         *
         * OpenedByAccountId har med flit ingen frammande nyckel. Som audit-raden ska
         * kallelsen overleva att tranarens konto raderas (§KM.6) -- vem som kallade ar en
         * anteckning, inte en agare. Kallelsen tillhor matchen.
         */
        builder.HasOne<Match>()
            .WithMany()
            .HasForeignKey(c => c.MatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

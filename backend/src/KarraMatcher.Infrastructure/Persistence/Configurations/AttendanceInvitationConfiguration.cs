using KarraMatcher.Domain.Attendance;
using KarraMatcher.Domain.Children;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KarraMatcher.Infrastructure.Persistence.Configurations;

internal sealed class AttendanceInvitationConfiguration
    : IEntityTypeConfiguration<AttendanceInvitation>
{
    public void Configure(EntityTypeBuilder<AttendanceInvitation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(i => i.Id);

        builder.Property(i => i.RespondedUtc).HasColumnType("timestamp with time zone");

        // Text och inte siffra: en siffra säger ingenting den dag någon felsöker med psql.
        builder.Property(i => i.Reply)
            .HasConversion<string>()
            .HasMaxLength(20);

        // Ett barn kallas en gång per kallelse; indexet är garantin mot dubbletter och
        // tjänar summeringen per kallelse via sitt CallId-prefix.
        builder.HasIndex(i => new { i.CallId, i.ChildId }).IsUnique();

        /*
         * Kaskad fran bada hallen. Fran kallelsen: en inbjudan utan kallelse hor ingenstans.
         * Fran barnet: nar ett barn tas bort ur truppen (§KM.6) ska dess kallelse-rader folja
         * med -- en rad som pekar pa ett borttaget barn ar inte raderad, den ar bara svar att
         * hitta. RespondedByAccountId har med flit ingen frammande nyckel (som AttendanceCall).
         */
        builder.HasOne<AttendanceCall>()
            .WithMany()
            .HasForeignKey(i => i.CallId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Child>()
            .WithMany()
            .HasForeignKey(i => i.ChildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

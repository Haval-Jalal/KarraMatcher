using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Attendance;
using KarraMatcher.Domain.Events;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KarraMatcher.Infrastructure.Persistence.Configurations;

internal sealed class AttendanceResponseConfiguration : IEntityTypeConfiguration<AttendanceResponse>
{
    public void Configure(EntityTypeBuilder<AttendanceResponse> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(r => r.Id);

        builder.Property(r => r.CreatedUtc).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(r => r.UpdatedUtc).HasColumnType("timestamp with time zone").IsRequired();

        builder.Property(r => r.Count).IsRequired();

        // Text och inte siffra: en siffra sager ingenting den dag nagon felsoker med psql.
        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        /*
         * Ett svar per konto och match. Kontrollen i tjansten uppdaterar det befintliga
         * svaret; indexet ar garantin mot att tva samtidiga anrop skapar tva. Det tjanar
         * ocksa summeringen per match (`#58`) via sitt MatchId-prefix.
         */
        builder.HasIndex(r => new { r.MatchId, r.AccountId }).IsUnique();

        /*
         * Kaskad fran bada hallen. Fran matchen: ett svar pa en match som inte finns ar
         * inget. Fran kontot: §KM.6 kraver att en radering tar med sig allt kontot ager, och
         * narvarosvaren hor dit.
         */
        builder.HasOne<Event>()
            .WithMany()
            .HasForeignKey(r => r.MatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(r => r.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

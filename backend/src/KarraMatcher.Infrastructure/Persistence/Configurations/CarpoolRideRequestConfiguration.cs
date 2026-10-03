using KarraMatcher.Domain.Carpool;
using KarraMatcher.Domain.Events;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KarraMatcher.Infrastructure.Persistence.Configurations;

internal sealed class CarpoolRideRequestConfiguration : IEntityTypeConfiguration<CarpoolRideRequest>
{
    public void Configure(EntityTypeBuilder<CarpoolRideRequest> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(r => r.Id);

        builder.Property(r => r.CreatedUtc).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(r => r.UpdatedUtc).HasColumnType("timestamp with time zone").IsRequired();

        builder.Property(r => r.Note).HasMaxLength(500);

        // Text och inte siffra: en siffra säger ingenting den dag någon felsöker med psql.
        builder.Property(r => r.Direction)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(CarpoolRideRequestStatus.Open)
            .IsRequired();

        // IsOpen är en räkenskap, inte en kolumn — härleds ur Status.
        builder.Ignore(r => r.IsOpen);

        // Den vanligaste frågan: en matchs öppna åkförfrågningar.
        builder.HasIndex(r => new { r.MatchId, r.Status });

        /*
         * Kaskad från båda håll (§KM.6). Från händelsen: en åkförfrågan till en match som inte
         * finns är ingenting. Från kontot: en radering tar med sig allt kontot äger — och
         * platserbjudandena kaskaderar i sin tur från åkförfrågan (se den andra konfigurationen).
         */
        builder.HasOne<Event>()
            .WithMany()
            .HasForeignKey(r => r.MatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Domain.Accounts.Account>()
            .WithMany()
            .HasForeignKey(r => r.RequesterAccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

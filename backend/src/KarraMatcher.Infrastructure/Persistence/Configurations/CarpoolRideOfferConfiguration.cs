using KarraMatcher.Domain.Carpool;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KarraMatcher.Infrastructure.Persistence.Configurations;

internal sealed class CarpoolRideOfferConfiguration : IEntityTypeConfiguration<CarpoolRideOffer>
{
    public void Configure(EntityTypeBuilder<CarpoolRideOffer> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(o => o.Id);

        builder.Property(o => o.CreatedUtc).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(o => o.UpdatedUtc).HasColumnType("timestamp with time zone").IsRequired();

        builder.Property(o => o.Message).HasMaxLength(500);

        // Förälderns svar tillbaka. Samma tak som hälsningen — ett nej ska rymma ett riktigt skäl.
        builder.Property(o => o.ResponseMessage).HasMaxLength(500);

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(CarpoolRequestStatus.Pending)
            .IsRequired();

        // IsActive är en räkenskap, inte en kolumn.
        builder.Ignore(o => o.IsActive);

        // Den enda frågan som ställs: en åkförfrågans platserbjudanden.
        builder.HasIndex(o => new { o.RideRequestId, o.Status });

        /*
         * Ett aktivt platserbjudande per förare och åkförfrågan — som ett filtrerat unikt index,
         * inte bara som en kontroll i tjänsten. Kontrollen ger det begripliga felet; indexet ger
         * garantin mot två samtidiga skrivningar. Ett nekat eller återtaget erbjudande blockerar
         * inte ett nytt försök.
         */
        builder.HasIndex(o => new { o.RideRequestId, o.DriverAccountId })
            .IsUnique()
            .HasFilter("\"Status\" IN ('Pending', 'Accepted')");

        /*
         * Kaskad från båda håll (§KM.6). Från åkförfrågan: ett platserbjudande på en förfrågan som
         * inte finns är ingenting. Från kontot: en radering tar med sig allt kontot äger.
         */
        builder.HasOne<CarpoolRideRequest>()
            .WithMany()
            .HasForeignKey(o => o.RideRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Domain.Accounts.Account>()
            .WithMany()
            .HasForeignKey(o => o.DriverAccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

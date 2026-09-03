using KarraMatcher.Domain.Carpool;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KarraMatcher.Infrastructure.Persistence.Configurations;

internal sealed class CarpoolOfferConfiguration : IEntityTypeConfiguration<CarpoolOffer>
{
    public void Configure(EntityTypeBuilder<CarpoolOffer> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(o => o.Id);

        // timestamptz, inte timestamp. Npgsql kräver då att Kind är Utc och kastar annars
        // — samma vakthund för §KM.5 som matchernas avspark har.
        builder.Property(o => o.DepartureUtc).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(o => o.CreatedUtc).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(o => o.UpdatedUtc).HasColumnType("timestamp with time zone").IsRequired();

        builder.Property(o => o.DeparturePlace).HasMaxLength(120).IsRequired();
        builder.Property(o => o.Note).HasMaxLength(500);

        // Text och inte siffra: en siffra i databasen säger ingenting den dag någon
        // felsöker med psql klockan sju en lördagmorgon.
        builder.Property(o => o.Direction).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(CarpoolOfferStatus.Open)
            .IsRequired();

        // Den enda frågan som ställs: erbjudandena för en match.
        builder.HasIndex(o => new { o.MatchId, o.Status });

        /*
         * Kaskad fran bada hallen, och bada behovs.
         *
         * Fran matchen: en borttagen match ska inte lamna kvar erbjudanden om skjuts till
         * nagot som inte finns. Fran kontot: §KM.6 kraver att en radering tar med sig allt
         * kontot ager, inklusive samakningserbjudanden -- och en rad som blir kvar med ett
         * id som pekar pa ingenting ar inte raderad, den ar bara svar att hitta.
         */
        builder.HasOne<Domain.Matches.Match>()
            .WithMany()
            .HasForeignKey(o => o.MatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Domain.Accounts.Account>()
            .WithMany()
            .HasForeignKey(o => o.DriverAccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

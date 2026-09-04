using KarraMatcher.Domain.Carpool;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KarraMatcher.Infrastructure.Persistence.Configurations;

internal sealed class CarpoolRequestConfiguration : IEntityTypeConfiguration<CarpoolRequest>
{
    public void Configure(EntityTypeBuilder<CarpoolRequest> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(r => r.Id);

        builder.Property(r => r.CreatedUtc).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(r => r.UpdatedUtc).HasColumnType("timestamp with time zone").IsRequired();

        builder.Property(r => r.Message).HasMaxLength(500);

        // Text och inte siffra: en siffra sager ingenting den dag nagon felsoker med psql.
        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(CarpoolRequestStatus.Pending)
            .IsRequired();

        /*
         * IsActive ar en rakenskap, inte en kolumn -- den harleds ur Status och far inte
         * hamna i databasen som ett andra stalle som vet samma sak.
         */
        builder.Ignore(r => r.IsActive);

        // Den enda fragan som stalls: ett erbjudandes forfragningar.
        builder.HasIndex(r => new { r.OfferId, r.Status });

        /*
         * En aktiv forfragan per person och erbjudande -- som ett filtrerat unikt index,
         * inte bara som en kontroll i handlern.
         *
         * Kontrollen i koden ger det begripliga felet; indexet ger garantin. Tva anrop som
         * kommer samtidigt hinner bada lasa "ingen aktiv forfragan finns" innan nagon av
         * dem skrivit, och da ar det bara databasen som kan saga nej.
         *
         * Filtret betyder att en nekad eller atertagen forfragan inte blockerar: planerna
         * kan ha andrats, och att lasa ute nagon for att de fragat en gang vore fel.
         */
        builder.HasIndex(r => new { r.OfferId, r.RequesterAccountId })
            .IsUnique()
            .HasFilter("\"Status\" IN ('Pending', 'Accepted')");

        /*
         * Kaskad fran bada hallen. Fran erbjudandet: en forfragan om en skjuts som inte
         * finns ar inget. Fran kontot: §KM.6 kraver att en radering tar med sig allt kontot
         * ager, och forfragningarna hor dit.
         */
        builder.HasOne<CarpoolOffer>()
            .WithMany()
            .HasForeignKey(r => r.OfferId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Domain.Accounts.Account>()
            .WithMany()
            .HasForeignKey(r => r.RequesterAccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

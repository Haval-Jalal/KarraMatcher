using KarraMatcher.Domain.Push;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KarraMatcher.Infrastructure.Persistence.Configurations;

internal sealed class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(s => s.Id);

        /*
         * Adressen ar lang. 2048 ar tilltaget for att rymma vad Apple, Google och Mozilla
         * skickar utan att bli en obegransad textkolumn -- en obegransad kolumn ar en
         * inbjudan att lagra nagot annat dar en dag.
         */
        builder.Property(s => s.Endpoint).HasMaxLength(2048).IsRequired();
        builder.Property(s => s.P256dh).HasMaxLength(256).IsRequired();
        builder.Property(s => s.Auth).HasMaxLength(128).IsRequired();

        builder.Property(s => s.CreatedUtc).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(s => s.LastUsedUtc).HasColumnType("timestamp with time zone");

        /*
         * En prenumeration per webblasare (`#332`-uppfoljning). Indexet ligger pa adressen ensam,
         * inte pa (lag, adress) som forr: en webblasare har en enda push-adress, sa en rad per lag
         * gav samma enhet flera identiska notiser. Nu upsertar prenumerationen pa adressen -> en rad
         * per enhet -> en notis. Postgres btree tar 2048 tecken (gransen gar vid ~2700 byte/rad).
         */
        builder.HasIndex(s => s.Endpoint).IsUnique();

        // Utskicket valjer mottagare pa konto (medlemskap), sa kontokolumnen behover sitt index.
        builder.HasIndex(s => s.AccountId);

        builder.HasOne<Domain.Teams.Team>()
            .WithMany()
            .HasForeignKey(s => s.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        /*
         * SetNull och inte Cascade (#63, beslut 2026-09-14). Raderas kontot bryts kopplingen,
         * men prenumerationen bor kvar: den hor till enheten, inte till kontot, och enheten
         * ska fortsatta fa lagets matchnotiser aven utan konto (§KM.3, §KM.6). Att radera hela
         * raden vore att tyst ta bort en notis enheten sjalv bett om.
         */
        builder.HasOne<Domain.Accounts.Account>()
            .WithMany()
            .HasForeignKey(s => s.AccountId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

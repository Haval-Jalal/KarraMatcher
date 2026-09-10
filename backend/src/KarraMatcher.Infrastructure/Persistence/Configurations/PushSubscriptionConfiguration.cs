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
         * En prenumeration per lag och webblasare. Utan det unika indexet hade varje
         * omladdning av sidan kunnat lagga till en till rad, och foraldern fatt sex notiser
         * for samma flyttade match.
         *
         * Indexet ligger pa hela adressen. Postgres btree tar 2048 tecken utan att klaga
         * har -- gransen gar vid ungefar 2700 byte per rad.
         */
        builder.HasIndex(s => new { s.TeamId, s.Endpoint }).IsUnique();

        // Utskicket fragar efter ett lags prenumerationer. Det ar den enda listningen.
        builder.HasIndex(s => s.TeamId);

        builder.HasOne<Domain.Teams.Team>()
            .WithMany()
            .HasForeignKey(s => s.TeamId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

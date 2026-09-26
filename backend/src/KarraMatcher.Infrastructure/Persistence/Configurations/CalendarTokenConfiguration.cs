using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Calendar;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KarraMatcher.Infrastructure.Persistence.Configurations;

internal sealed class CalendarTokenConfiguration : IEntityTypeConfiguration<CalendarToken>
{
    public void Configure(EntityTypeBuilder<CalendarToken> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(t => t.Id);

        // Base64url av 32 byte blir 43 tecken; 64 ger marginal.
        builder.Property(t => t.Token).HasMaxLength(64).IsRequired();

        // Feeden slår upp exakt en rad på nyckeln. Unikt — två rader med samma nyckel vore
        // samma feed till två konton.
        builder.HasIndex(t => t.Token).IsUnique();

        // En nyckel per konto: visning och återkallande slår upp på kontot.
        builder.HasIndex(t => t.AccountId).IsUnique();

        builder.Property(t => t.CreatedUtc).IsRequired();

        // Ägs av kontot och försvinner med det (§KM.6) — kaskad, som allt annat mot ett konto.
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

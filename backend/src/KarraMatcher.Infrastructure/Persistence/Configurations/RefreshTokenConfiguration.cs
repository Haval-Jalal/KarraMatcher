using KarraMatcher.Domain.Accounts;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KarraMatcher.Infrastructure.Persistence.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(t => t.Id);

        // SHA-256 i hex: alltid 64 tecken.
        builder.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();

        // Varje förnyelse slår upp exakt en token på hashen. Unikt, eftersom två rader med
        // samma hash vore samma token — och då vet vi inte vilken som är den giltiga.
        builder.HasIndex(t => t.TokenHash).IsUnique();

        // Återkallandet av en familj är den vanligaste skrivningen efter en upptäckt
        // stöld, och sker under en request som en användare väntar på.
        builder.HasIndex(t => t.FamilyId);

        builder.Property(t => t.CreatedUtc).IsRequired();
        builder.Property(t => t.ExpiresUtc).IsRequired();
    }
}

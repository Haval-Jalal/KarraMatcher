using KarraMatcher.Domain.Accounts;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KarraMatcher.Infrastructure.Persistence.Configurations;

internal sealed class PasskeyConfiguration : IEntityTypeConfiguration<Passkey>
{
    public void Configure(EntityTypeBuilder<Passkey> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(p => p.Id);

        builder.Property(p => p.CredentialId).IsRequired();

        // Inloggningen slår upp exakt en passkey på credential-id:t. Unikt — två rader med samma
        // id vore två konton bakom samma nyckel.
        builder.HasIndex(p => p.CredentialId).IsUnique();

        builder.Property(p => p.PublicKey).IsRequired();
        builder.Property(p => p.DeviceLabel).HasMaxLength(60);
        builder.Property(p => p.CreatedUtc).IsRequired();

        builder.HasIndex(p => p.AccountId);

        // Ägs av kontot och försvinner med det (§KM.6) — kaskad, som allt annat mot ett konto.
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(p => p.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

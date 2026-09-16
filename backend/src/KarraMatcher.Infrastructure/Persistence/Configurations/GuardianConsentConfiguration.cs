using KarraMatcher.Domain.Consent;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KarraMatcher.Infrastructure.Persistence.Configurations;

internal sealed class GuardianConsentConfiguration : IEntityTypeConfiguration<GuardianConsent>
{
    public void Configure(EntityTypeBuilder<GuardianConsent> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Version).HasMaxLength(20).IsRequired();
        builder.Property(c => c.GrantedUtc).HasColumnType("timestamp with time zone");

        // Ett konto samtycker till en viss version högst en gång.
        builder.HasIndex(c => new { c.AccountId, c.Version }).IsUnique();

        // Raderas kontot försvinner dess samtycken (§KM.6).
        builder.HasOne(c => c.Account)
            .WithMany()
            .HasForeignKey(c => c.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

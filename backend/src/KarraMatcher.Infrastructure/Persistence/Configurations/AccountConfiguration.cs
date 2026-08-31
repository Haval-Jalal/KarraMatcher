using KarraMatcher.Domain.Accounts;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KarraMatcher.Infrastructure.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(a => a.Id);

        // 320 tecken är den längsta adress standarden tillåter (64 + @ + 255).
        builder.Property(a => a.Email).HasMaxLength(320).IsRequired();

        // Unik på adressen, som lagras normaliserad till gemener. Utan indexet hade två
        // konton för samma person kunnat uppstå genom en enda inloggning med versal.
        builder.HasIndex(a => a.Email).IsUnique();

        builder.Property(a => a.CreatedUtc).IsRequired();

        // Kaskad: raderas kontot ska dess tokens följa med i samma svep (checklistan 1.6).
        builder.HasMany(a => a.RefreshTokens)
            .WithOne(t => t.Account!)
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

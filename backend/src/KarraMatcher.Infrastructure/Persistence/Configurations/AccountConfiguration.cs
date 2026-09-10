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

        /*
         * Namnet ar valfritt i databasen aven om formularet kraver ett fornamn. Konton som
         * skapades innan #154 har inget, och en NOT NULL hade tvingat fram ett pahittat
         * varde for dem -- vilket ar samre an att veta att det saknas.
         */
        builder.Property(a => a.FirstName).HasMaxLength(60);
        builder.Property(a => a.LastName).HasMaxLength(60);

        // Harlett, inte lagrat.
        builder.Ignore(a => a.DisplayName);

        builder.Property(a => a.CreatedUtc).IsRequired();

        // Kaskad: raderas kontot ska dess tokens följa med i samma svep (checklistan 1.6).
        builder.HasMany(a => a.RefreshTokens)
            .WithOne(t => t.Account!)
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

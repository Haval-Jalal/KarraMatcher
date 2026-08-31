using KarraMatcher.Domain.Accounts;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KarraMatcher.Infrastructure.Persistence.Configurations;

internal sealed class LoginCodeConfiguration : IEntityTypeConfiguration<LoginCode>
{
    public void Configure(EntityTypeBuilder<LoginCode> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Email).HasMaxLength(320).IsRequired();
        builder.Property(c => c.CodeHash).HasMaxLength(64).IsRequired();
        builder.Property(c => c.CreatedUtc).IsRequired();
        builder.Property(c => c.ExpiresUtc).IsRequired();

        // Varje begaran och varje verifiering slar upp pa adress och tar den senaste.
        // Utan indexet vaxer den uppslagningen med tabellen, som aldrig gallras hart.
        builder.HasIndex(c => new { c.Email, c.CreatedUtc });
    }
}

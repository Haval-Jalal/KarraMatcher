using KarraMatcher.Domain.Audit;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KarraMatcher.Infrastructure.Persistence.Configurations;

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Action).HasMaxLength(64).IsRequired();
        builder.Property(e => e.OccurredUtc).IsRequired();

        // Ingen frammande nyckel till Accounts -- posten om en radering ska finnas kvar
        // efter att kontot ar borta. Se AuditEntry.
        builder.HasIndex(e => e.OccurredUtc);
    }
}

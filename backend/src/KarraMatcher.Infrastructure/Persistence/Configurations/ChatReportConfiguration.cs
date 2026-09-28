using KarraMatcher.Domain.Chat;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KarraMatcher.Infrastructure.Persistence.Configurations;

internal sealed class ChatReportConfiguration : IEntityTypeConfiguration<ChatReport>
{
    public void Configure(EntityTypeBuilder<ChatReport> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(r => r.Id);

        // Krypteras i vila (se DbContext.OnModelCreating), så kolumnen är `text` (obegränsad):
        // chiffret är längre än de MaxReason klartext-tecken som validatorn vaktar.
        builder.Property(r => r.Reason).IsRequired();

        builder.Property(r => r.CreatedUtc).HasColumnType("timestamp with time zone").IsRequired();

        // En medlem anmäler ett meddelande en gång — dubbelanmälan är ingen extra signal.
        builder.HasIndex(r => new { r.MessageId, r.ReportedByAccountId }).IsUnique();

        builder.HasOne<ChatMessage>()
            .WithMany()
            .HasForeignKey(r => r.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        // Kaskad från kontot (§KM.6).
        builder.HasOne<Domain.Accounts.Account>()
            .WithMany()
            .HasForeignKey(r => r.ReportedByAccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

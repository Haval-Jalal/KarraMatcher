using KarraMatcher.Domain.Push;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KarraMatcher.Infrastructure.Persistence.Configurations;

internal sealed class NotificationPreferenceConfiguration
    : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(p => p.Id);

        builder.Property(p => p.MatchChanges).IsRequired();
        builder.Property(p => p.Carpool).IsRequired();
        builder.Property(p => p.Reminders).IsRequired();
        builder.Property(p => p.UpdatedUtc).HasColumnType("timestamp with time zone").IsRequired();

        // En rad per konto och lag. Utan det unika indexet kunde tva sparningar lagga tva
        // rader, och utskicket lasa den ena eller den andra.
        builder.HasIndex(p => new { p.AccountId, p.TeamId }).IsUnique();

        // Kaskad fran bada: §KM.6 (en radering tar med sig kontots installningar) och fran
        // laget (ett lag som tas bort lamnar inga foraldralosa preferenser).
        builder.HasOne<Domain.Accounts.Account>()
            .WithMany()
            .HasForeignKey(p => p.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Domain.Teams.Team>()
            .WithMany()
            .HasForeignKey(p => p.TeamId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

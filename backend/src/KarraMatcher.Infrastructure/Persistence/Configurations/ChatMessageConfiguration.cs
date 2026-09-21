using KarraMatcher.Domain.Chat;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KarraMatcher.Infrastructure.Persistence.Configurations;

internal sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    /// <summary>Taket på ett meddelande. Prövas server-side, inte bara i formuläret.</summary>
    public const int MaxBodyLength = 2000;

    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(m => m.Id);

        // Tom efter radering (tombstone), så inte required. Taket gäller ändå.
        builder.Property(m => m.Body).HasMaxLength(MaxBodyLength);

        builder.Property(m => m.CreatedUtc).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(m => m.PublishAtUtc).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(m => m.PublishedUtc).HasColumnType("timestamp with time zone");
        builder.Property(m => m.DeletedUtc).HasColumnType("timestamp with time zone");

        // Kanalens meddelanden i tidsordning — den vanligaste frågan.
        builder.HasIndex(m => new { m.AgeGroupId, m.TeamId, m.PublishAtUtc });

        // Släpp-jobbet: schemalagda som passerat sin tid.
        builder.HasIndex(m => new { m.PublishedUtc, m.PublishAtUtc });

        // Kaskad från truppen och (för lag-kanaler, `#202`) laget: en kanal som tas bort
        // lämnar inga föräldralösa meddelanden.
        builder.HasOne<Domain.Teams.AgeGroup>()
            .WithMany()
            .HasForeignKey(m => m.AgeGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Domain.Teams.Team>()
            .WithMany()
            .HasForeignKey(m => m.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        // Kaskad från kontot: §KM.6 — en radering tar med sig det kontot skrivit.
        builder.HasOne<Domain.Accounts.Account>()
            .WithMany()
            .HasForeignKey(m => m.AuthorAccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

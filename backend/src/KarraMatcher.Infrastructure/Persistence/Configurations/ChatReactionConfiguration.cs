using KarraMatcher.Domain.Chat;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KarraMatcher.Infrastructure.Persistence.Configurations;

internal sealed class ChatReactionConfiguration : IEntityTypeConfiguration<ChatReaction>
{
    public void Configure(EntityTypeBuilder<ChatReaction> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Emoji).HasMaxLength(ChatReaction.MaxEmoji).IsRequired();

        builder.Property(r => r.CreatedUtc).HasColumnType("timestamp with time zone").IsRequired();

        // Ett konto reagerar med en viss emoji en gång — en tryckning växlar av/på.
        builder.HasIndex(r => new { r.MessageId, r.ReactedByAccountId, r.Emoji }).IsUnique();

        builder.HasOne<ChatMessage>()
            .WithMany()
            .HasForeignKey(r => r.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        // Kaskad från kontot (§KM.6).
        builder.HasOne<Domain.Accounts.Account>()
            .WithMany()
            .HasForeignKey(r => r.ReactedByAccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

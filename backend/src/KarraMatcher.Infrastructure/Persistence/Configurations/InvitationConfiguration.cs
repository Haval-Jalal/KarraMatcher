using KarraMatcher.Domain.Invitations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KarraMatcher.Infrastructure.Persistence.Configurations;

internal sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Email).HasMaxLength(320).IsRequired();
        builder.Property(i => i.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(i => i.CreatedUtc).HasColumnType("timestamp with time zone");
        builder.Property(i => i.ExpiresUtc).HasColumnType("timestamp with time zone");
        builder.Property(i => i.AcceptedUtc).HasColumnType("timestamp with time zone");

        // Uppslagningen vid accept sker på token-hashen; unik så samma hash aldrig delas.
        builder.HasIndex(i => i.TokenHash).IsUnique();

        // Medlemskaps- och listuppslag sker per trupp och status.
        builder.HasIndex(i => new { i.AgeGroupId, i.Status });

        // Raderas truppen försvinner dess inbjudningar.
        builder.HasOne(i => i.AgeGroup)
            .WithMany()
            .HasForeignKey(i => i.AgeGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        // Det valfria lag-förslaget nollställs om laget tas bort.
        builder.HasOne(i => i.Team)
            .WithMany()
            .HasForeignKey(i => i.TeamId)
            .OnDelete(DeleteBehavior.SetNull);

        // Raderas det konto som accepterat försvinner medlemskapet med det (§KM.6).
        builder.HasOne(i => i.AcceptedByAccount)
            .WithMany()
            .HasForeignKey(i => i.AcceptedByAccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

using KarraMatcher.Domain.Applications;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KarraMatcher.Infrastructure.Persistence.Configurations;

internal sealed class MembershipApplicationConfiguration
    : IEntityTypeConfiguration<MembershipApplication>
{
    public void Configure(EntityTypeBuilder<MembershipApplication> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(a => a.Id);

        builder.Property(a => a.CreatedUtc).HasColumnType("timestamp with time zone");
        builder.Property(a => a.ResolvedUtc).HasColumnType("timestamp with time zone");

        // Kön och medlemskapsuppslag sker per trupp och status.
        builder.HasIndex(a => new { a.AgeGroupId, a.Status });

        // En sökande syns snabbt när hen ansöker igen (dubblettkoll).
        builder.HasIndex(a => new { a.AccountId, a.AgeGroupId });

        // Raderas truppen försvinner dess ansökningar.
        builder.HasOne(a => a.AgeGroup)
            .WithMany()
            .HasForeignKey(a => a.AgeGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        // Raderas kontot försvinner dess ansökningar (§KM.6).
        builder.HasOne(a => a.Account)
            .WithMany()
            .HasForeignKey(a => a.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

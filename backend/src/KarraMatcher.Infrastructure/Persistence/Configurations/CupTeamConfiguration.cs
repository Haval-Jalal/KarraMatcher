using KarraMatcher.Domain.Cup;
using KarraMatcher.Domain.Events;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KarraMatcher.Infrastructure.Persistence.Configurations;

internal sealed class CupTeamConfiguration : IEntityTypeConfiguration<CupTeam>
{
    public void Configure(EntityTypeBuilder<CupTeam> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(team => team.Id);

        builder.Property(team => team.Name).HasMaxLength(CupTeam.MaxName).IsRequired();

        builder.Property(team => team.CreatedUtc).HasColumnType("timestamp with time zone").IsRequired();

        builder.HasIndex(team => team.EventId);

        // Kaskad från cup-händelsen: raderas cupen försvinner dess cup-lag.
        builder.HasOne<Event>()
            .WithMany()
            .HasForeignKey(team => team.EventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

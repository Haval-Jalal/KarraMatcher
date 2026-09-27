using KarraMatcher.Domain.Children;
using KarraMatcher.Domain.Cup;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KarraMatcher.Infrastructure.Persistence.Configurations;

internal sealed class CupTeamMemberConfiguration : IEntityTypeConfiguration<CupTeamMember>
{
    public void Configure(EntityTypeBuilder<CupTeamMember> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(member => member.Id);

        builder.Property(member => member.CreatedUtc)
            .HasColumnType("timestamp with time zone").IsRequired();

        // Ett barn placeras en gång i ett visst cup-lag. Att barnet inte hamnar i två lag i
        // samma cup vaktas i tjänsten (kräver cupens id, som inte ligger på raden).
        builder.HasIndex(member => new { member.CupTeamId, member.ChildId }).IsUnique();

        // Kaskad från cup-laget.
        builder.HasOne<CupTeam>()
            .WithMany()
            .HasForeignKey(member => member.CupTeamId)
            .OnDelete(DeleteBehavior.Cascade);

        // Kaskad från barnet (§KM.6): placeringen försvinner när barnet tas bort ur truppen.
        builder.HasOne<Child>()
            .WithMany()
            .HasForeignKey(member => member.ChildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

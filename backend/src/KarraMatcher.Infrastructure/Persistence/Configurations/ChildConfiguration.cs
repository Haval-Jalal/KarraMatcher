using KarraMatcher.Domain.Children;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KarraMatcher.Infrastructure.Persistence.Configurations;

internal sealed class ChildConfiguration : IEntityTypeConfiguration<Child>
{
    public void Configure(EntityTypeBuilder<Child> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(c => c.Id);
        builder.Property(c => c.FirstName).HasMaxLength(50).IsRequired();

        // Bara en initial (§KM.1). Ett kort tak markerar avsikten även i schemat: här ryms
        // inget efternamn.
        builder.Property(c => c.LastInitial).HasMaxLength(2).IsRequired();

        builder.Property(c => c.CreatedUtc).HasColumnType("timestamp with time zone");

        // Truppen: raderas truppen försvinner barnen med den.
        builder.HasOne(c => c.AgeGroup)
            .WithMany()
            .HasForeignKey(c => c.AgeGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        // Laget (färgen): valfritt. Raderas ett lag blir barnen osorterade, inte raderade.
        builder.HasOne(c => c.Team)
            .WithMany()
            .HasForeignKey(c => c.TeamId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(c => c.AgeGroupId);
        builder.HasIndex(c => c.TeamId);
    }
}

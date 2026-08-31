using KarraMatcher.Domain.Accounts;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KarraMatcher.Infrastructure.Persistence.Configurations;

internal sealed class TeamRoleConfiguration : IEntityTypeConfiguration<TeamRole>
{
    public void Configure(EntityTypeBuilder<TeamRole> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Role).HasConversion<int>().IsRequired();
        builder.Property(r => r.GrantedUtc).IsRequired();

        builder.HasOne(r => r.Account)
            .WithMany()
            .HasForeignKey(r => r.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        // Laget raderas inte under fötterna på en roll som pekar på det.
        builder.HasOne(r => r.Team)
            .WithMany()
            .HasForeignKey(r => r.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        // Samma roll for samma konto och lag ska bara kunna finnas en gang.
        builder.HasIndex(r => new { r.AccountId, r.TeamId, r.Role }).IsUnique();

        /*
         * Villkoret i databasen och inte bara i koden: en tranare utan lag skulle bli en
         * tranare for alla lag, och en admin med ett lag skulle se ut att vara begransad
         * utan att vara det. Bada ar tysta behorighetsfel, och bada ar lattare att skriva
         * av misstag an att upptacka.
         */
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_TeamRoles_LagKravsForTranare",
            """("Role" = 1 AND "TeamId" IS NOT NULL) OR ("Role" = 2 AND "TeamId" IS NULL)"""));
    }
}

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

        // Truppen på samma sätt (v2): en admin-roll pekar på en trupp.
        builder.HasOne(r => r.AgeGroup)
            .WithMany()
            .HasForeignKey(r => r.AgeGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        // Samma roll for samma konto och scope ska bara kunna finnas en gang.
        builder.HasIndex(r => new { r.AccountId, r.TeamId, r.AgeGroupId, r.Role }).IsUnique();

        /*
         * Villkoret i databasen och inte bara i koden (v2): rollens scope maste stamma med
         * dess sort. En tranare utan lag skulle bli tranare for alla, en admin utan trupp en
         * global admin, en superadmin med scope en begransad superadmin -- alla tysta
         * behorighetsfel, lattare att skriva av misstag an att upptacka.
         *   Coach(1)      -> lag satt, trupp tom
         *   Admin(2)      -> trupp satt, lag tom
         *   SuperAdmin(3) -> bada tomma (global)
         */
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_TeamRoles_ScopePassarRollen",
            """
            ("Role" = 1 AND "TeamId" IS NOT NULL AND "AgeGroupId" IS NULL)
            OR ("Role" = 2 AND "AgeGroupId" IS NOT NULL AND "TeamId" IS NULL)
            OR ("Role" = 3 AND "TeamId" IS NULL AND "AgeGroupId" IS NULL)
            """));
    }
}

using KarraMatcher.Application.Features.Consent;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Children;
using KarraMatcher.Domain.Consent;
using KarraMatcher.Domain.Teams;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Seed;

/// <summary>
/// Demodata för testning (`#267`): en trupp-admin, en vårdnadshavare med samtycke och några
/// barn, så hela kedjan (kallelse → svar per barn → samåkning → chatt) går att prova utan att
/// någon riktig familj läggs in.
///
/// <para>
/// Allt är config-styrt och avstängt som standard — precis som superadmin-seeden. Adresserna
/// hårdkodas aldrig i repot; inloggningen är kodlös per mejl, så peka dem på adresser du kan ta
/// emot post på (t.ex. plus-adresser till din egen inkorg). Idempotent som resten av seeden, och
/// <c>DemoSeed:Clear=true</c> tar bort exakt det som seedats igen.
/// </para>
/// </summary>
public sealed partial class DatabaseSeeder
{
    public const string DemoEnabledKey = "DemoSeed:Enabled";
    public const string DemoClearKey = "DemoSeed:Clear";
    public const string DemoAdminEmailKey = "DemoSeed:AdminEmail";
    public const string DemoGuardianEmailKey = "DemoSeed:GuardianEmail";

    /// <summary>Barnen är minimala (§KM.1): förnamn + efternamnets initial, inget mer.</summary>
    private static readonly (string First, string LastInitial)[] DemoChildren =
    [
        ("Liam", "J"),
        ("Nova", "S"),
        ("Elias", "B"),
    ];

    private const string DemoTeamSlug = "gul";
    private const string DemoAdminName = "Demo Admin";
    private const string DemoGuardianName = "Demo Förälder";

    private async Task EnsureDemoAsync(
        AgeGroup ageGroup,
        Dictionary<string, Team> teams,
        CancellationToken cancellationToken)
    {
        var adminEmail = configuration[DemoAdminEmailKey]?.Trim().ToLowerInvariant();
        var guardianEmail = configuration[DemoGuardianEmailKey]?.Trim().ToLowerInvariant();
        var clear = bool.TryParse(configuration[DemoClearKey], out var c) && c;
        var enabled = bool.TryParse(configuration[DemoEnabledKey], out var e) && e;

        if (!teams.TryGetValue(DemoTeamSlug, out var team))
        {
            return;
        }

        if (clear)
        {
            await ClearDemoAsync(team, adminEmail, guardianEmail, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!enabled || string.IsNullOrEmpty(adminEmail) || string.IsNullOrEmpty(guardianEmail))
        {
            return;
        }

        var admin = await EnsureAccountAsync(adminEmail, DemoAdminName, cancellationToken)
            .ConfigureAwait(false);
        await EnsureAdminRoleAsync(admin, ageGroup, cancellationToken).ConfigureAwait(false);

        var guardian = await EnsureAccountAsync(guardianEmail, DemoGuardianName, cancellationToken)
            .ConfigureAwait(false);
        await EnsureConsentAsync(guardian, cancellationToken).ConfigureAwait(false);

        foreach (var (first, lastInitial) in DemoChildren)
        {
            var child = await EnsureChildAsync(first, lastInitial, ageGroup, team, cancellationToken)
                .ConfigureAwait(false);
            await EnsureGuardianshipAsync(guardian, child, cancellationToken).ConfigureAwait(false);
        }

        // Gör kallelsen testbar på demolaget (den levereras annars avstängd, §KM.7).
        if (!team.AttendanceEnabled)
        {
            team.AttendanceEnabled = true;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<Account> EnsureAccountAsync(
        string email, string firstName, CancellationToken cancellationToken)
    {
        var account = await context.Accounts
            .FirstOrDefaultAsync(a => a.Email == email, cancellationToken)
            .ConfigureAwait(false);

        if (account is not null)
        {
            return account;
        }

        account = new Account { Email = email, FirstName = firstName, CreatedUtc = DateTime.UtcNow };
        context.Accounts.Add(account);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return account;
    }

    private async Task EnsureAdminRoleAsync(
        Account admin, AgeGroup ageGroup, CancellationToken cancellationToken)
    {
        var hasRole = await context.TeamRoles
            .AnyAsync(
                r => r.AccountId == admin.Id
                    && r.AgeGroupId == ageGroup.Id
                    && r.Role == RoleKind.Admin,
                cancellationToken)
            .ConfigureAwait(false);

        if (!hasRole)
        {
            context.TeamRoles.Add(new TeamRole
            {
                AccountId = admin.Id,
                AgeGroupId = ageGroup.Id,
                Role = RoleKind.Admin,
                GrantedUtc = DateTime.UtcNow,
            });
        }
    }

    private async Task EnsureConsentAsync(Account guardian, CancellationToken cancellationToken)
    {
        var hasConsent = await context.GuardianConsents
            .AnyAsync(
                gc => gc.AccountId == guardian.Id && gc.Version == ConsentDocument.CurrentVersion,
                cancellationToken)
            .ConfigureAwait(false);

        if (!hasConsent)
        {
            context.GuardianConsents.Add(new GuardianConsent
            {
                AccountId = guardian.Id,
                Version = ConsentDocument.CurrentVersion,
                GrantedUtc = DateTime.UtcNow,
            });
        }
    }

    private async Task<Child> EnsureChildAsync(
        string first, string lastInitial, AgeGroup ageGroup, Team team, CancellationToken cancellationToken)
    {
        var child = await context.Children
            .FirstOrDefaultAsync(
                x => x.AgeGroupId == ageGroup.Id
                    && x.TeamId == team.Id
                    && x.FirstName == first
                    && x.LastInitial == lastInitial,
                cancellationToken)
            .ConfigureAwait(false);

        if (child is not null)
        {
            return child;
        }

        child = new Child
        {
            FirstName = first,
            LastInitial = lastInitial,
            AgeGroupId = ageGroup.Id,
            TeamId = team.Id,
            CreatedUtc = DateTime.UtcNow,
        };
        context.Children.Add(child);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return child;
    }

    private async Task EnsureGuardianshipAsync(
        Account guardian, Child child, CancellationToken cancellationToken)
    {
        var linked = await context.Guardianships
            .AnyAsync(
                g => g.AccountId == guardian.Id && g.ChildId == child.Id,
                cancellationToken)
            .ConfigureAwait(false);

        if (!linked)
        {
            context.Guardianships.Add(new Guardianship
            {
                AccountId = guardian.Id,
                ChildId = child.Id,
                GrantedUtc = DateTime.UtcNow,
            });
        }
    }

    /// <summary>
    /// Tar bort exakt det demodata som seedats: vårdnadshavarens barn och kopplingar, adminens
    /// roll och samtycket, samt de två kontona — och stänger av kallelsen på demolaget igen.
    /// Explicita borttagningar (inte förlitan på kaskad) så det fungerar lika mot vilken
    /// provider som helst.
    /// </summary>
    private async Task ClearDemoAsync(
        Team team, string? adminEmail, string? guardianEmail, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(guardianEmail))
        {
            var guardian = await context.Accounts
                .FirstOrDefaultAsync(a => a.Email == guardianEmail, cancellationToken)
                .ConfigureAwait(false);

            if (guardian is not null)
            {
                var guardianships = await context.Guardianships
                    .Where(g => g.AccountId == guardian.Id)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var childIds = guardianships.Select(g => g.ChildId).ToList();

                var children = await context.Children
                    .Where(x => childIds.Contains(x.Id))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var consents = await context.GuardianConsents
                    .Where(gc => gc.AccountId == guardian.Id)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                context.Guardianships.RemoveRange(guardianships);
                context.Children.RemoveRange(children);
                context.GuardianConsents.RemoveRange(consents);
                context.Accounts.Remove(guardian);
            }
        }

        if (!string.IsNullOrEmpty(adminEmail))
        {
            var admin = await context.Accounts
                .FirstOrDefaultAsync(a => a.Email == adminEmail, cancellationToken)
                .ConfigureAwait(false);

            if (admin is not null)
            {
                var roles = await context.TeamRoles
                    .Where(r => r.AccountId == admin.Id)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                context.TeamRoles.RemoveRange(roles);
                context.Accounts.Remove(admin);
            }
        }

        if (team.AttendanceEnabled)
        {
            team.AttendanceEnabled = false;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

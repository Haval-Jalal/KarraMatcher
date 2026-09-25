using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Applications;
using KarraMatcher.Domain.Invitations;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

/// <summary>
/// Avgör medlemskap mot databasen (v2, `#191`). Auktoritativt: läser roller och
/// vårdnadshavarkopplingar, inte en token som kan vara inaktuell.
/// </summary>
internal sealed class MembershipService(KarraMatcherDbContext context) : IMembershipService
{
    public async Task<bool> IsMemberOfTeamAsync(
        Guid accountId, Guid teamId, CancellationToken cancellationToken)
    {
        var team = await context.Teams
            .AsNoTracking()
            .Where(t => t.Id == teamId)
            .Select(t => new { t.Id, t.AgeGroupId })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return team is not null
            && await IsMemberCoreAsync(accountId, team.Id, team.AgeGroupId, cancellationToken)
                .ConfigureAwait(false);
    }

    public async Task<bool> IsMemberOfTeamBySlugAsync(
        Guid accountId, string teamSlug, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(teamSlug);

        var team = await context.Teams
            .AsNoTracking()
            .Where(t => t.Slug == teamSlug)
            .Select(t => new { t.Id, t.AgeGroupId })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return team is not null
            && await IsMemberCoreAsync(accountId, team.Id, team.AgeGroupId, cancellationToken)
                .ConfigureAwait(false);
    }

    public Task<Guid?> TruppIdForTeamBySlugAsync(
        string teamSlug, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(teamSlug);

        return context.Teams
            .AsNoTracking()
            .Where(t => t.Slug == teamSlug)
            .Select(t => (Guid?)t.AgeGroupId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> IsMemberOfEventAsync(
        Guid accountId, Guid eventId, CancellationToken cancellationToken)
    {
        var team = await context.Events
            .AsNoTracking()
            .Where(m => m.Id == eventId)
            .Select(m => new { TeamId = m.TeamId, m.Team!.AgeGroupId })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return team is not null
            && await IsMemberCoreAsync(accountId, team.TeamId, team.AgeGroupId, cancellationToken)
                .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> MemberTeamSlugsAsync(
        Guid accountId, CancellationToken cancellationToken)
    {
        var roles = await context.TeamRoles
            .AsNoTracking()
            .Where(r => r.AccountId == accountId)
            .Select(r => new { r.Role, r.TeamId, r.AgeGroupId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Superadmin ser alla lag.
        if (roles.Exists(r => r.Role == RoleKind.SuperAdmin))
        {
            return await context.Teams.AsNoTracking()
                .Select(t => t.Slug)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        var coachTeamIds = roles.Where(r => r.Role == RoleKind.Coach && r.TeamId != null)
            .Select(r => r.TeamId!.Value).ToHashSet();
        var ageGroupIds = roles.Where(r => r.Role == RoleKind.Admin && r.AgeGroupId != null)
            .Select(r => r.AgeGroupId!.Value).ToHashSet();

        var guardianTeamIds = await context.Guardianships
            .AsNoTracking()
            .Where(g => g.AccountId == accountId && g.Child!.TeamId != null)
            .Select(g => g.Child!.TeamId!.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Accepterade inbjudningar ger medlemskap i truppen (v2, `#193`), alltså i alla dess lag.
        var invitedAgeGroupIds = await context.Invitations
            .AsNoTracking()
            .Where(i => i.AcceptedByAccountId == accountId && i.Status == InvitationStatus.Accepted)
            .Select(i => i.AgeGroupId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        ageGroupIds.UnionWith(invitedAgeGroupIds);

        // Godkända ansökningar likaså (v2, `#194`).
        var approvedAgeGroupIds = await context.MembershipApplications
            .AsNoTracking()
            .Where(a => a.AccountId == accountId && a.Status == ApplicationStatus.Approved)
            .Select(a => a.AgeGroupId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        ageGroupIds.UnionWith(approvedAgeGroupIds);

        var teamIds = coachTeamIds.Concat(guardianTeamIds).ToHashSet();

        return await context.Teams
            .AsNoTracking()
            .Where(t => teamIds.Contains(t.Id) || ageGroupIds.Contains(t.AgeGroupId))
            .Select(t => t.Slug)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MemberTruppDto>> MemberTrupperAsync(
        Guid accountId, CancellationToken cancellationToken)
    {
        var ageGroupIds = new HashSet<Guid>();

        // Ledarskap (admin för truppen eller tränare för något av dess lag) → får schemalägga.
        var leaderAgeGroupIds = new HashSet<Guid>();

        var adminAgeGroupIds = await context.TeamRoles
            .AsNoTracking()
            .Where(r => r.AccountId == accountId && r.Role == RoleKind.Admin && r.AgeGroupId != null)
            .Select(r => r.AgeGroupId!.Value)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var coachAgeGroupIds = await context.TeamRoles
            .AsNoTracking()
            .Where(r => r.AccountId == accountId && r.Role == RoleKind.Coach && r.Team != null)
            .Select(r => r.Team!.AgeGroupId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        leaderAgeGroupIds.UnionWith(adminAgeGroupIds);
        leaderAgeGroupIds.UnionWith(coachAgeGroupIds);
        ageGroupIds.UnionWith(leaderAgeGroupIds);

        ageGroupIds.UnionWith(await context.Guardianships
            .AsNoTracking()
            .Where(g => g.AccountId == accountId)
            .Select(g => g.Child!.AgeGroupId)
            .ToListAsync(cancellationToken).ConfigureAwait(false));

        ageGroupIds.UnionWith(await context.Invitations
            .AsNoTracking()
            .Where(i => i.AcceptedByAccountId == accountId && i.Status == InvitationStatus.Accepted)
            .Select(i => i.AgeGroupId)
            .ToListAsync(cancellationToken).ConfigureAwait(false));

        ageGroupIds.UnionWith(await context.MembershipApplications
            .AsNoTracking()
            .Where(a => a.AccountId == accountId && a.Status == ApplicationStatus.Approved)
            .Select(a => a.AgeGroupId)
            .ToListAsync(cancellationToken).ConfigureAwait(false));

        if (ageGroupIds.Count == 0)
        {
            return [];
        }

        var trupper = await context.AgeGroups
            .AsNoTracking()
            .Include(a => a.Club)
            .Where(a => ageGroupIds.Contains(a.Id))
            .OrderBy(a => a.Club!.Name).ThenBy(a => a.Name).ThenBy(a => a.Season)
            .Select(a => new { a.Id, ClubName = a.Club!.Name, a.Name, a.Season })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. trupper.Select(a => new MemberTruppDto(
                a.Id, a.ClubName, a.Name, a.Season, leaderAgeGroupIds.Contains(a.Id))),
        ];
    }

    public Task<bool> IsLeaderOfTruppAsync(
        Guid accountId, Guid ageGroupId, CancellationToken cancellationToken) =>
        context.TeamRoles
            .AsNoTracking()
            .AnyAsync(
                r => r.AccountId == accountId
                    && (r.Role == RoleKind.SuperAdmin
                        || (r.Role == RoleKind.Admin && r.AgeGroupId == ageGroupId)
                        || (r.Role == RoleKind.Coach && r.Team!.AgeGroupId == ageGroupId)),
                cancellationToken);

    public async Task<bool> IsMemberOfTruppAsync(
        Guid accountId, Guid ageGroupId, CancellationToken cancellationToken)
    {
        var hasRole = await context.TeamRoles
            .AsNoTracking()
            .AnyAsync(
                r => r.AccountId == accountId
                    && (r.Role == RoleKind.SuperAdmin
                        || (r.Role == RoleKind.Admin && r.AgeGroupId == ageGroupId)
                        || (r.Role == RoleKind.Coach && r.Team!.AgeGroupId == ageGroupId)),
                cancellationToken)
            .ConfigureAwait(false);

        if (hasRole)
        {
            return true;
        }

        var isGuardian = await context.Guardianships
            .AsNoTracking()
            .AnyAsync(g => g.AccountId == accountId && g.Child!.AgeGroupId == ageGroupId, cancellationToken)
            .ConfigureAwait(false);

        if (isGuardian)
        {
            return true;
        }

        var invited = await context.Invitations
            .AsNoTracking()
            .AnyAsync(
                i => i.AcceptedByAccountId == accountId
                    && i.AgeGroupId == ageGroupId
                    && i.Status == InvitationStatus.Accepted,
                cancellationToken)
            .ConfigureAwait(false);

        if (invited)
        {
            return true;
        }

        return await context.MembershipApplications
            .AsNoTracking()
            .AnyAsync(
                a => a.AccountId == accountId
                    && a.AgeGroupId == ageGroupId
                    && a.Status == ApplicationStatus.Approved,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> MemberAccountIdsForTruppAsync(
        Guid ageGroupId, CancellationToken cancellationToken)
    {
        var ids = new HashSet<Guid>();

        // Admins för truppen och tränare för något av dess lag. Global superadmin utelämnas.
        ids.UnionWith(await context.TeamRoles
            .AsNoTracking()
            .Where(r => (r.Role == RoleKind.Admin && r.AgeGroupId == ageGroupId)
                || (r.Role == RoleKind.Coach && r.Team!.AgeGroupId == ageGroupId))
            .Select(r => r.AccountId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false));

        // Vårdnadshavare till barn i truppen (tvärs över lagen).
        ids.UnionWith(await context.Guardianships
            .AsNoTracking()
            .Where(g => g.Child!.AgeGroupId == ageGroupId)
            .Select(g => g.AccountId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false));

        ids.UnionWith(await context.Invitations
            .AsNoTracking()
            .Where(i => i.Status == InvitationStatus.Accepted
                && i.AgeGroupId == ageGroupId
                && i.AcceptedByAccountId != null)
            .Select(i => i.AcceptedByAccountId!.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false));

        ids.UnionWith(await context.MembershipApplications
            .AsNoTracking()
            .Where(a => a.Status == ApplicationStatus.Approved && a.AgeGroupId == ageGroupId)
            .Select(a => a.AccountId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false));

        return [.. ids];
    }

    public async Task<IReadOnlyList<Guid>> MemberAccountIdsAsync(
        Guid teamId, CancellationToken cancellationToken)
    {
        var ageGroupId = await context.Teams
            .AsNoTracking()
            .Where(t => t.Id == teamId)
            .Select(t => (Guid?)t.AgeGroupId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (ageGroupId is null)
        {
            return [];
        }

        var age = ageGroupId.Value;
        var ids = new HashSet<Guid>();

        // Tränare för laget och admins för dess trupp. Global superadmin utelämnas med flit —
        // en lag-notis ska inte nå plattformsägaren för varje lag.
        ids.UnionWith(await context.TeamRoles
            .AsNoTracking()
            .Where(r => (r.Role == RoleKind.Coach && r.TeamId == teamId)
                || (r.Role == RoleKind.Admin && r.AgeGroupId == age))
            .Select(r => r.AccountId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false));

        // Vårdnadshavare till barn i laget.
        ids.UnionWith(await context.Guardianships
            .AsNoTracking()
            .Where(g => g.Child!.TeamId == teamId)
            .Select(g => g.AccountId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false));

        // Accepterade inbjudningar och godkända ansökningar ger trupp-medlemskap (`#193`/`#194`).
        ids.UnionWith(await context.Invitations
            .AsNoTracking()
            .Where(i => i.Status == InvitationStatus.Accepted
                && i.AgeGroupId == age
                && i.AcceptedByAccountId != null)
            .Select(i => i.AcceptedByAccountId!.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false));

        ids.UnionWith(await context.MembershipApplications
            .AsNoTracking()
            .Where(a => a.Status == ApplicationStatus.Approved && a.AgeGroupId == age)
            .Select(a => a.AccountId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false));

        return [.. ids];
    }

    public Task<string?> TruppNameAsync(Guid ageGroupId, CancellationToken cancellationToken) =>
        context.AgeGroups
            .AsNoTracking()
            .Where(a => a.Id == ageGroupId)
            .Select(a => a.Name)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<TeamChannelInfo>> AccessibleTeamChannelsAsync(
        Guid accountId, Guid ageGroupId, CancellationToken cancellationToken)
    {
        var truppWide = await HasTruppWideAccessAsync(accountId, ageGroupId, cancellationToken)
            .ConfigureAwait(false);

        var teams = context.Teams.AsNoTracking().Where(t => t.AgeGroupId == ageGroupId);

        if (!truppWide)
        {
            // Utan trupp-bred åtkomst: bara de lag kontot är tränare för eller har ett barn i.
            // Samma gräns som MemberOfTeam (IsMemberCoreAsync) drar per lag.
            var coachTeamIds = await context.TeamRoles
                .AsNoTracking()
                .Where(r => r.AccountId == accountId
                    && r.Role == RoleKind.Coach
                    && r.TeamId != null
                    && r.Team!.AgeGroupId == ageGroupId)
                .Select(r => r.TeamId!.Value)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var guardianTeamIds = await context.Guardianships
                .AsNoTracking()
                .Where(g => g.AccountId == accountId
                    && g.Child!.AgeGroupId == ageGroupId
                    && g.Child!.TeamId != null)
                .Select(g => g.Child!.TeamId!.Value)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var ids = coachTeamIds.Concat(guardianTeamIds).ToHashSet();
            teams = teams.Where(t => ids.Contains(t.Id));
        }

        return await teams
            .OrderBy(t => t.Name)
            .Select(t => new TeamChannelInfo(t.Id, t.Slug, t.Name, t.ColorHex))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Når kontot <em>alla</em> lag i truppen? Sant för superadmin, admin för truppen, samt en
    /// accepterad inbjudan eller godkänd ansökan till truppen — samma trupp-breda grenar som
    /// <see cref="IsMemberCoreAsync"/> släpper igenom oavsett lag.
    /// </summary>
    private async Task<bool> HasTruppWideAccessAsync(
        Guid accountId, Guid ageGroupId, CancellationToken cancellationToken)
    {
        var hasRole = await context.TeamRoles
            .AsNoTracking()
            .AnyAsync(
                r => r.AccountId == accountId
                    && (r.Role == RoleKind.SuperAdmin
                        || (r.Role == RoleKind.Admin && r.AgeGroupId == ageGroupId)),
                cancellationToken)
            .ConfigureAwait(false);

        if (hasRole)
        {
            return true;
        }

        var invited = await context.Invitations
            .AsNoTracking()
            .AnyAsync(
                i => i.AcceptedByAccountId == accountId
                    && i.AgeGroupId == ageGroupId
                    && i.Status == InvitationStatus.Accepted,
                cancellationToken)
            .ConfigureAwait(false);

        if (invited)
        {
            return true;
        }

        return await context.MembershipApplications
            .AsNoTracking()
            .AnyAsync(
                a => a.AccountId == accountId
                    && a.AgeGroupId == ageGroupId
                    && a.Status == ApplicationStatus.Approved,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<bool> IsMemberCoreAsync(
        Guid accountId, Guid teamId, Guid ageGroupId, CancellationToken cancellationToken)
    {
        var hasRole = await context.TeamRoles
            .AsNoTracking()
            .AnyAsync(
                r => r.AccountId == accountId
                    && (r.Role == RoleKind.SuperAdmin
                        || (r.Role == RoleKind.Admin && r.AgeGroupId == ageGroupId)
                        || (r.Role == RoleKind.Coach && r.TeamId == teamId)),
                cancellationToken)
            .ConfigureAwait(false);

        if (hasRole)
        {
            return true;
        }

        var isGuardian = await context.Guardianships
            .AsNoTracking()
            .AnyAsync(
                g => g.AccountId == accountId && g.Child!.TeamId == teamId,
                cancellationToken)
            .ConfigureAwait(false);

        if (isGuardian)
        {
            return true;
        }

        // En accepterad inbjudan till truppen är också ett medlemskap (v2, `#193`) — en
        // förälder ser sitt lags trupp redan innan barnet kopplats (§KM.1, `#196`).
        var invited = await context.Invitations
            .AsNoTracking()
            .AnyAsync(
                i => i.AcceptedByAccountId == accountId
                    && i.AgeGroupId == ageGroupId
                    && i.Status == InvitationStatus.Accepted,
                cancellationToken)
            .ConfigureAwait(false);

        if (invited)
        {
            return true;
        }

        // En godkänd ansökan är samma sorts medlemskap som en accepterad inbjudan (v2, `#194`).
        return await context.MembershipApplications
            .AsNoTracking()
            .AnyAsync(
                a => a.AccountId == accountId
                    && a.AgeGroupId == ageGroupId
                    && a.Status == ApplicationStatus.Approved,
                cancellationToken)
            .ConfigureAwait(false);
    }
}

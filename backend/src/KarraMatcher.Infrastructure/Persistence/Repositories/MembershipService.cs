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

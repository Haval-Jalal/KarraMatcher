using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Applications;
using KarraMatcher.Domain.Events;
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
        var ev = await context.Events
            .AsNoTracking()
            .Where(m => m.Id == eventId)
            .Select(m => new { m.Type, TeamId = m.TeamId, m.AgeGroupId })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (ev is null)
        {
            return false;
        }

        // En match syns bara för den som sköter laget/truppen eller vars barn är *kallat* — inte
        // för hela truppen (§KM.7, ägarbeslut). Träning/cup/övrigt är fortsatt synligt för alla
        // trupp-/lag-medlemmar. Kallelsen är planering; en match man inte är kallad på ska inte ens
        // gå att öppna.
        if (ev.Type == EventType.Match)
        {
            if (await HasManagerRoleAsync(accountId, ev.AgeGroupId, cancellationToken)
                .ConfigureAwait(false))
            {
                return true;
            }

            return await IsGuardianOfInvitedChildAsync(accountId, eventId, cancellationToken)
                .ConfigureAwait(false);
        }

        if (await IsMemberCoreAsync(accountId, ev.TeamId, ev.AgeGroupId, cancellationToken)
            .ConfigureAwait(false))
        {
            return true;
        }

        // En vårdnadshavare vars barn faktiskt är *kallat* till händelsen får öppna den — även om
        // barnet tillhör ett annat färg-lag eller ännu inget lag (§KM.7 tillåter kallelse tvärs
        // över lagen). Utan detta fick föräldern 403 på just den sida push-notisen och Hem-kortet
        // ledde till (#378). Gäller alla MemberOfEvent-ytor: matchdetaljen och samåkningen.
        return await IsGuardianOfInvitedChildAsync(accountId, eventId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Sant om kontot är vårdnadshavare för ett barn som har en kallelse-inbjudan till händelsen.
    /// Kallelsen (<see cref="AttendanceInvitation"/>) pekar på ett barn, inte på ett lag, så den
    /// bär rätten att se just den händelsen oavsett lagtillhörighet.
    /// </summary>
    private Task<bool> IsGuardianOfInvitedChildAsync(
        Guid accountId, Guid eventId, CancellationToken cancellationToken) =>
        (from invitation in context.AttendanceInvitations.AsNoTracking()
         join call in context.AttendanceCalls on invitation.CallId equals call.Id
         join guardianship in context.Guardianships on invitation.ChildId equals guardianship.ChildId
         where call.MatchId == eventId && guardianship.AccountId == accountId
         select invitation.Id)
        .AnyAsync(cancellationToken);

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

        // En accepterad inbjudan / godkänd ansökan ger medlemskap i *truppen*, inte i ett bestämt
        // färg-lag — så den ger inga lag i lagväljaren här. Lagen dyker upp när barnet placerats
        // (guardianTeamIds). Att unionera invited/approved på AgeGroup listade förr alla färg-lag
        // för en inbjuden förälder, vilket (ihop med gate-buggen) gav åtkomst till andra lags
        // schema/chatt (#579, §KM.3). Admin-rollen (ageGroupIds ovan) ser fortsatt alla trupps lag.
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
            // En tränare för något av truppens färg-lag ser ALLA lag-kanaler i sin trupp
            // (ägarbeslut 2026-09-28): färg-lagen är indelningar av samma trupp, och tränarna
            // leder truppen. En ren vårdnadshavare ser däremot bara sitt eget barns lag (§KM.3).
            var coachesAnyTeamInTrupp = await context.TeamRoles
                .AsNoTracking()
                .AnyAsync(
                    r => r.AccountId == accountId
                        && r.Role == RoleKind.Coach
                        && r.TeamId != null
                        && r.Team!.AgeGroupId == ageGroupId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!coachesAnyTeamInTrupp)
            {
                var guardianTeamIds = await context.Guardianships
                    .AsNoTracking()
                    .Where(g => g.AccountId == accountId
                        && g.Child!.AgeGroupId == ageGroupId
                        && g.Child!.TeamId != null)
                    .Select(g => g.Child!.TeamId!.Value)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var ids = guardianTeamIds.ToHashSet();
                teams = teams.Where(t => ids.Contains(t.Id));
            }
        }

        return await teams
            .OrderBy(t => t.Name)
            .Select(t => new TeamChannelInfo(t.Id, t.Slug, t.Name, t.ColorHex))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Når kontot <em>alla</em> lag i truppen? Sant bara för superadmin och admin för truppen.
    ///
    /// <para>
    /// En inbjuden eller ansökande förälder är visserligen trupp-medlem — hen ser truppchatten
    /// via <see cref="IsMemberCoreAsync"/> — men ska <b>inte</b> se alla färg-lags kanaler eller
    /// händelser, bara sitt eget barns lag (§KM.3). Därför räknas varken inbjudan eller ansökan
    /// som trupp-bred här; det är den avsiktliga skillnaden mot <c>IsMemberCoreAsync</c>. En
    /// tränare når sina egna lag via coach-grenen i <see cref="AccessibleTeamChannelsAsync"/>.
    /// </para>
    /// </summary>
    /// <summary>
    /// Sköter kontot truppen — superadmin, admin för truppen, eller tränare för något av dess lag?
    /// Den som sköter laget/truppen ser alla dess matcher (behöver planera innan kallelse), tvärs
    /// över färg-lagen (rollmodellen: tränare leder truppen). Vårdnadshavare gör det inte (`#514`).
    /// </summary>
    private Task<bool> HasManagerRoleAsync(
        Guid accountId, Guid ageGroupId, CancellationToken cancellationToken) =>
        context.TeamRoles
            .AsNoTracking()
            .AnyAsync(
                r => r.AccountId == accountId
                    && (r.Role == RoleKind.SuperAdmin
                        || (r.Role == RoleKind.Admin && r.AgeGroupId == ageGroupId)
                        || (r.Role == RoleKind.Coach && r.Team!.AgeGroupId == ageGroupId)),
                cancellationToken);

    public async Task<MatchVisibility> GetMatchVisibilityAsync(
        Guid accountId, CancellationToken cancellationToken)
    {
        // Rollerna en gång: superadmin (ser allt), och trupperna kontot är admin eller tränare i.
        var roles = await context.TeamRoles
            .AsNoTracking()
            .Where(r => r.AccountId == accountId)
            .Select(r => new
            {
                r.Role,
                r.AgeGroupId,
                TeamAgeGroupId = r.Team != null ? (Guid?)r.Team.AgeGroupId : null,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var isSuperAdmin = roles.Any(r => r.Role == RoleKind.SuperAdmin);

        var managerAgeGroupIds = new HashSet<Guid>();
        foreach (var role in roles)
        {
            if (role.Role == RoleKind.Admin && role.AgeGroupId is { } adminAgeGroup)
            {
                managerAgeGroupIds.Add(adminAgeGroup);
            }
            else if (role.Role == RoleKind.Coach && role.TeamAgeGroupId is { } coachAgeGroup)
            {
                managerAgeGroupIds.Add(coachAgeGroup);
            }
        }

        // Matcherna kontots barn är kallat till (via kallelse-inbjudan). AttendanceCall.MatchId är
        // händelse-id:t. Distinkt: ett konto kan ha flera barn kallade till samma match.
        var calledMatchEventIds = await (
            from invitation in context.AttendanceInvitations.AsNoTracking()
            join call in context.AttendanceCalls on invitation.CallId equals call.Id
            join guardianship in context.Guardianships on invitation.ChildId equals guardianship.ChildId
            where guardianship.AccountId == accountId
            select call.MatchId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new MatchVisibility(isSuperAdmin, managerAgeGroupIds, calledMatchEventIds.ToHashSet());
    }

    private Task<bool> HasTruppWideAccessAsync(
        Guid accountId, Guid ageGroupId, CancellationToken cancellationToken) =>
        context.TeamRoles
            .AsNoTracking()
            .AnyAsync(
                r => r.AccountId == accountId
                    && (r.Role == RoleKind.SuperAdmin
                        || (r.Role == RoleKind.Admin && r.AgeGroupId == ageGroupId)),
                cancellationToken);

    // teamId == null = en trupp-vid händelse: medlem är då den som hör till truppen alls
    // (vilken tränare eller vårdnadshavare som helst i truppen), inte bara ett visst lag.
    private async Task<bool> IsMemberCoreAsync(
        Guid accountId, Guid? teamId, Guid ageGroupId, CancellationToken cancellationToken)
    {
        var hasRole = await context.TeamRoles
            .AsNoTracking()
            .AnyAsync(
                r => r.AccountId == accountId
                    && (r.Role == RoleKind.SuperAdmin
                        || (r.Role == RoleKind.Admin && r.AgeGroupId == ageGroupId)
                        || (r.Role == RoleKind.Coach
                            && (teamId == null ? r.Team!.AgeGroupId == ageGroupId : r.TeamId == teamId))),
                cancellationToken)
            .ConfigureAwait(false);

        if (hasRole)
        {
            return true;
        }

        var isGuardian = await context.Guardianships
            .AsNoTracking()
            .AnyAsync(
                g => g.AccountId == accountId
                    && (teamId == null ? g.Child!.AgeGroupId == ageGroupId : g.Child!.TeamId == teamId),
                cancellationToken)
            .ConfigureAwait(false);

        if (isGuardian)
        {
            return true;
        }

        // En accepterad inbjudan / godkänd ansökan ger medlemskap i *truppen* (v2, `#193`/`#194`) —
        // inte i ett bestämt färg-lag. De grinderna gäller därför bara trupp-nivå-anrop (teamId ==
        // null). För ett lag-scopat anrop (teamId != null) avgör roll eller vårdnadshavarskap för
        // just det laget; annars fick en inbjuden/godkänd förälder åtkomst till *alla* färg-lags
        // chatt och schema i truppen (#579, §KM.3). Lag-medlemskapet kommer när barnet placerats i
        // laget och ger vårdnadshavar-grenen ovan. Trupp-chatt/aktivitetslista går via
        // IsMemberOfTruppAsync, som fortsatt räknar inbjudna/godkända.
        if (teamId == null)
        {
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

        return false;
    }
}

using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Attendance;
using KarraMatcher.Domain.Events;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

internal sealed class AttendanceCallRepository(KarraMatcherDbContext context)
    : IAttendanceCallRepository
{
    /// <summary>
    /// Neutral färg när det saknas ett lag att ta färgen ifrån: en trupp-vid cup (TeamId == null,
    /// `#332`/#552) eller ett otilldelat barn i en kallelse (Child.TeamId == null, #553). Ett värde
    /// i stället för den null som annars läckt in i de icke-nullbara/visade fälten.
    /// </summary>
    private const string NoTeamColorHex = "#6B6B6B";

    /// <summary>Gruppnamn för ett barn som ännu inte lagts i ett färg-lag (#553).</summary>
    private const string NoTeamName = "(inget lag)";


    public async Task<EventContext?> FindEventContextAsync(
        Guid eventId, CancellationToken cancellationToken) =>
        await context.Events
            .AsNoTracking()
            .Where(e => e.Id == eventId)
            .Select(e => new EventContext(e.AgeGroupId, e.TeamId, e.KickoffUtc, e.Type, e.Note))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<DateTime?> FindKickoffUtcAsync(
        Guid eventId, CancellationToken cancellationToken) =>
        await context.Events
            .AsNoTracking()
            .Where(e => e.Id == eventId)
            .Select(e => (DateTime?)e.KickoffUtc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    // Spårad: samma kallelse kan behöva få inbjudningar synkade i samma enhet av arbete.
    public Task<AttendanceCall?> FindCallByEventAsync(Guid eventId, CancellationToken cancellationToken) =>
        context.AttendanceCalls.FirstOrDefaultAsync(c => c.MatchId == eventId, cancellationToken);

    public async Task AddCallAsync(AttendanceCall attendanceCall, CancellationToken cancellationToken) =>
        await context.AttendanceCalls.AddAsync(attendanceCall, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlySet<Guid>> ChildIdsInTruppAsync(
        Guid ageGroupId, CancellationToken cancellationToken)
    {
        var ids = await context.Children
            .AsNoTracking()
            .Where(c => c.AgeGroupId == ageGroupId)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ids.ToHashSet();
    }

    public async Task<IReadOnlyList<AttendanceInvitation>> ListInvitationsAsync(
        Guid callId, CancellationToken cancellationToken) =>
        await context.AttendanceInvitations
            .Where(i => i.CallId == callId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AddInvitationAsync(
        AttendanceInvitation invitation, CancellationToken cancellationToken) =>
        await context.AttendanceInvitations.AddAsync(invitation, cancellationToken).ConfigureAwait(false);

    public void RemoveInvitation(AttendanceInvitation invitation) =>
        context.AttendanceInvitations.Remove(invitation);

    public Task<int> CountComingAsync(Guid callId, CancellationToken cancellationToken) =>
        context.AttendanceInvitations
            .AsNoTracking()
            .CountAsync(
                i => i.CallId == callId && i.Reply == AttendanceReply.Coming,
                cancellationToken);

    public Task<AttendanceInvitation?> FindInvitationAsync(
        Guid callId, Guid childId, CancellationToken cancellationToken) =>
        context.AttendanceInvitations
            .FirstOrDefaultAsync(i => i.CallId == callId && i.ChildId == childId, cancellationToken);

    public async Task<IReadOnlyList<InvitationRow>> ListInvitationRowsAsync(
        Guid callId, CancellationToken cancellationToken) =>
        await context.AttendanceInvitations
            .AsNoTracking()
            .Where(i => i.CallId == callId)
            .Join(
                context.Children.AsNoTracking(),
                i => i.ChildId,
                c => c.Id,
                // Child.TeamId är legitimt null (otilldelat barn). c.Team!.Name hade gett null via
                // LEFT JOIN rakt in i summeringen → en tom grupp utan färg hos tränaren (#553).
                // Fall tillbaka på en namngiven sentinel-grupp + neutral färg i stället.
                (i, c) => new InvitationRow(
                    c.Id,
                    c.FirstName,
                    c.LastInitial,
                    c.Team != null ? c.Team.Name : NoTeamName,
                    c.Team != null ? c.Team.ColorHex : NoTeamColorHex,
                    i.Reply))
            .OrderBy(r => r.TeamName).ThenBy(r => r.FirstName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<MyInvitationRow>> ListMineAsync(
        Guid eventId, Guid accountId, CancellationToken cancellationToken) =>
        await (
            from i in context.AttendanceInvitations.AsNoTracking()
            join call in context.AttendanceCalls.AsNoTracking() on i.CallId equals call.Id
            join c in context.Children.AsNoTracking() on i.ChildId equals c.Id
            join g in context.Guardianships.AsNoTracking() on c.Id equals g.ChildId
            where call.MatchId == eventId && g.AccountId == accountId
            orderby c.FirstName
            select new MyInvitationRow(c.Id, c.FirstName, c.LastInitial, i.Reply))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<MyCupChildRow>> MyCupChildrenAsync(
        Guid ageGroupId, Guid accountId, Guid callId, CancellationToken cancellationToken) =>
        await (
            from g in context.Guardianships.AsNoTracking()
            join c in context.Children.AsNoTracking() on g.ChildId equals c.Id
            where g.AccountId == accountId && c.AgeGroupId == ageGroupId
            orderby c.FirstName
            select new MyCupChildRow(
                c.Id,
                c.FirstName,
                c.LastInitial,
                context.AttendanceInvitations.Any(
                    i => i.CallId == callId
                        && i.ChildId == c.Id
                        && i.Reply == AttendanceReply.Coming)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<TruppCupRow>> ListTruppCupsAsync(
        Guid ageGroupId, CancellationToken cancellationToken)
    {
        // Vänster-joina laget: en trupp-vid cup (TeamId == null, `#332`) saknar lag. `e.Team!.Name`
        // gav en LEFT JOIN vars null matades rakt in i de icke-nullbara TeamName/ColorHex-fälten
        // (#552). Fall tillbaka på truppens namn + en neutral färg, som PendingKallelserAsync gör.
        var cups = await (
            from e in context.Events.AsNoTracking()
            join ageGroup in context.AgeGroups.AsNoTracking() on e.AgeGroupId equals ageGroup.Id
            join teamCandidate in context.Teams.AsNoTracking() on e.TeamId equals teamCandidate.Id
                into teamMatch
            from team in teamMatch.DefaultIfEmpty()
            where e.Type == EventType.Cup && e.AgeGroupId == ageGroupId
            orderby e.KickoffUtc
            select new
            {
                e.Id,
                e.Title,
                e.KickoffUtc,
                TeamName = team != null ? team.Name : ageGroup.Name,
                ColorHex = team != null ? team.ColorHex : NoTeamColorHex,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (cups.Count == 0)
        {
            return [];
        }

        var eventIds = cups.Select(c => c.Id).ToList();

        // Kallelsen (platstaket) per cup, och antal Ja per kallelse — två frågor, inga korrelerade
        // underfrågor, så EF-översättningen förblir enkel.
        var calls = await context.AttendanceCalls
            .AsNoTracking()
            .Where(c => eventIds.Contains(c.MatchId))
            .Select(c => new { c.Id, c.MatchId, c.Capacity })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var callByEvent = calls.ToDictionary(c => c.MatchId);
        var callIds = calls.Select(c => c.Id).ToList();

        var comingByCall = (await context.AttendanceInvitations
            .AsNoTracking()
            .Where(i => callIds.Contains(i.CallId) && i.Reply == AttendanceReply.Coming)
            .GroupBy(i => i.CallId)
            .Select(g => new { CallId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false))
            .ToDictionary(x => x.CallId, x => x.Count);

        return
        [
            .. cups.Select(cup =>
            {
                callByEvent.TryGetValue(cup.Id, out var call);
                var coming = call is not null && comingByCall.TryGetValue(call.Id, out var n) ? n : 0;

                return new TruppCupRow(
                    cup.Id, cup.Title, cup.KickoffUtc, cup.TeamName, cup.ColorHex, call?.Capacity, coming);
            }),
        ];
    }

    public Task<bool> IsGuardianOfChildAsync(
        Guid accountId, Guid childId, CancellationToken cancellationToken) =>
        context.Guardianships
            .AsNoTracking()
            .AnyAsync(g => g.AccountId == accountId && g.ChildId == childId, cancellationToken);

    public async Task<IReadOnlyList<Guid>> GuardianAccountIdsForChildrenAsync(
        IReadOnlyCollection<Guid> childIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(childIds);

        if (childIds.Count == 0)
        {
            return [];
        }

        return await context.Guardianships
            .AsNoTracking()
            .Where(g => childIds.Contains(g.ChildId))
            .Select(g => g.AccountId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}

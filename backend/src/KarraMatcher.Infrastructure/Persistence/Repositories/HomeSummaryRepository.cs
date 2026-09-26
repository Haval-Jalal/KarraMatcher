using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Events;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

/// <summary>
/// Läsningarna bakom Hem-vyn. Rena läsningar (<c>AsNoTracking</c>); "kommande" och "ej inställd"
/// uttrycks som i påminnelsejobbet, med UTC-nu från anroparen (§KM.5).
/// </summary>
internal sealed class HomeSummaryRepository(KarraMatcherDbContext context) : IHomeSummaryRepository
{
    public async Task<Event?> NextEventAsync(
        IReadOnlyCollection<Guid> teamIds,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        return await context.Events
            .AsNoTracking()
            .Include(e => e.Venue)
            .Include(e => e.Team!)
            .ThenInclude(team => team!.AgeGroup!)
            .ThenInclude(ageGroup => ageGroup!.Club)
            .Where(e => teamIds.Contains(e.TeamId)
                && e.KickoffUtc >= nowUtc
                && e.Status != EventStatus.Cancelled)
            .OrderBy(e => e.KickoffUtc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PendingKallelseRow>> PendingKallelserAsync(
        Guid accountId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        // En rad per kommande händelse där något av kontots egna barn saknar svar (Reply == null)
        // på en öppen kallelse i ett lag där kallelse är påslagen. Cup-anmälningar har alltid ett
        // svar och faller därför bort av sig själva.
        var query =
            from invitation in context.AttendanceInvitations.AsNoTracking()
            join call in context.AttendanceCalls.AsNoTracking() on invitation.CallId equals call.Id
            join item in context.Events.AsNoTracking() on call.MatchId equals item.Id
            join child in context.Children.AsNoTracking() on invitation.ChildId equals child.Id
            join guardianship in context.Guardianships.AsNoTracking()
                on child.Id equals guardianship.ChildId
            join team in context.Teams.AsNoTracking() on item.TeamId equals team.Id
            where guardianship.AccountId == accountId
                && invitation.Reply == null
                && item.KickoffUtc >= nowUtc
                && item.Status != EventStatus.Cancelled
                && team.AttendanceEnabled
            group invitation by new
            {
                item.Id,
                item.Type,
                item.KickoffUtc,
                item.Title,
                item.OpponentName,
                item.IsHome,
                TeamName = team.Name,
            }
            into grouped
            orderby grouped.Key.KickoffUtc
            select new PendingKallelseRow(
                grouped.Key.Id,
                grouped.Key.Type,
                grouped.Key.KickoffUtc,
                grouped.Key.Title,
                grouped.Key.OpponentName,
                grouped.Key.IsHome,
                grouped.Key.TeamName,
                grouped.Count());

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ChatLatestRow?> LatestChatAsync(
        IReadOnlyCollection<Guid> truppIds,
        IReadOnlyCollection<Guid> teamIds,
        CancellationToken cancellationToken)
    {
        // Kanalen är paret (trupp, ev. lag): primärkanalen (TeamId == null) för varje trupp
        // medlemmen är med i, plus de lag hen når. Ett lag hör alltid till sin trupp, så filtret
        // släpper aldrig in en kanal medlemmen inte ser.
        return await context.ChatMessages
            .AsNoTracking()
            .Where(m => truppIds.Contains(m.AgeGroupId)
                && (m.TeamId == null || teamIds.Contains(m.TeamId.Value))
                && m.PublishedUtc != null
                && m.DeletedUtc == null)
            .OrderByDescending(m => m.PublishAtUtc)
            .Select(m => new ChatLatestRow(m.AgeGroupId, m.TeamId, m.AuthorAccountId, m.Body, m.PublishAtUtc))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

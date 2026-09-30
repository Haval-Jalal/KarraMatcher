using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Calendar;
using KarraMatcher.Domain.Events;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

/// <summary>Kalender-nycklarna och feedens händelser (kalender bakom medlemskap, §KM.4).</summary>
internal sealed class CalendarRepository(
    KarraMatcherDbContext context,
    TimeProvider clock) : ICalendarRepository
{
    public async Task<CalendarToken?> FindByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken) =>
        await context.CalendarTokens
            .FirstOrDefaultAsync(t => t.AccountId == accountId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<Guid?> ResolveAccountAsync(string token, CancellationToken cancellationToken)
    {
        var row = await context.CalendarTokens
            .FirstOrDefaultAsync(t => t.Token == token, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        // En upplysning till kontot om att feeden faktiskt hämtas — inte en tidsstämpel någon
        // annan får se.
        row.LastUsedUtc = clock.GetUtcNow().UtcDateTime;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return row.AccountId;
    }

    public async Task AddAsync(CalendarToken token, CancellationToken cancellationToken) =>
        await context.CalendarTokens.AddAsync(token, cancellationToken).ConfigureAwait(false);

    public void Remove(CalendarToken token) => context.CalendarTokens.Remove(token);

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<Event>> EventsForTeamsAsync(
        IReadOnlyCollection<Guid> teamIds,
        IReadOnlyCollection<Guid> truppIds,
        DateTime fromUtc,
        CancellationToken cancellationToken) =>
        await context.Events
            .AsNoTracking()
            .Include(e => e.Venue)
            .Include(e => e.Team!)
            .ThenInclude(team => team!.AgeGroup!)
            .ThenInclude(ageGroup => ageGroup!.Club)
            // Trupp-vida händelser (utan lag) har ingen Team-navigering — ladda AgeGroup+klubb direkt
            // så namn och hemma-plats kan lösas även då (#386/#475).
            .Include(e => e.AgeGroup!)
            .ThenInclude(ageGroup => ageGroup!.Club)
            // Ett lag-riktat event i något av medlemmens lag, ELLER en trupp-övergripande händelse
            // (TeamId == null) i någon av medlemmens trupper (`#332`, #475) — samma urval som Hem.
            .Where(e => ((e.TeamId != null && teamIds.Contains(e.TeamId.Value))
                    || (e.TeamId == null && truppIds.Contains(e.AgeGroupId)))
                && e.KickoffUtc >= fromUtc)
            .OrderBy(e => e.KickoffUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}

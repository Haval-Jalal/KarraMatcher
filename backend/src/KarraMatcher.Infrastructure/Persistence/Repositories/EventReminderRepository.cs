using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Events;
using KarraMatcher.Domain.Events;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

internal sealed class EventReminderRepository(KarraMatcherDbContext context)
    : IEventReminderRepository
{
    public async Task<IReadOnlyList<DueEvent>> ListDueAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        // Plocka ut de nullbara plats-delarna null-säkert och lös själva platsen i minnet — samma
        // ordning som schemat/detaljsidan (TeamMapping.ResolveLocation): en legacy-spelplats
        // (VenueId), annars hemma → truppens hemmaplan, annars den skrivna adressen. App-skapade
        // händelser har inget VenueId sedan #307/#405, så `Venue.Name` ensamt gav en tom plats i
        // påminnelsen (#465).
        var rows = await context.Events
            .AsNoTracking()
            .Where(e => e.KickoffUtc >= fromUtc
                && e.KickoffUtc < toUtc
                && e.Status != EventStatus.Cancelled
                && e.ReminderSentUtc == null)
            .OrderBy(e => e.KickoffUtc)
            .Select(e => new
            {
                e.Id,
                e.TeamId,
                e.AgeGroupId,
                Type = e.Type.ToString(),
                e.KickoffUtc,
                e.Title,
                e.OpponentName,
                e.IsHome,
                VenueName = e.Venue != null ? e.Venue.Name : null,
                HomeVenueName = e.AgeGroup != null ? e.AgeGroup.HomeVenueName : null,
                e.AddressOverride,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(r => new DueEvent(
                r.Id,
                r.TeamId,
                r.AgeGroupId,
                r.Type,
                r.KickoffUtc,
                r.Title,
                r.OpponentName,
                r.IsHome,
                (r.VenueName ?? (r.IsHome == true ? r.HomeVenueName : r.AddressOverride))
                    ?? string.Empty))
            .ToList();
    }

    public async Task MarkRemindedAsync(
        IReadOnlyCollection<Guid> eventIds,
        DateTime sentUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventIds);

        if (eventIds.Count == 0)
        {
            return;
        }

        var reminded = await context.Events
            .Where(e => eventIds.Contains(e.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var item in reminded)
        {
            item.ReminderSentUtc = sentUtc;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

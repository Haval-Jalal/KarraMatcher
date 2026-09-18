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
        CancellationToken cancellationToken) =>
        await context.Events
            .AsNoTracking()
            .Where(e => e.KickoffUtc >= fromUtc
                && e.KickoffUtc < toUtc
                && e.Status != EventStatus.Cancelled
                && e.ReminderSentUtc == null)
            .OrderBy(e => e.KickoffUtc)
            .Select(e => new DueEvent(
                e.Id, e.TeamId, e.Type.ToString(), e.KickoffUtc, e.Title, e.OpponentName, e.IsHome, e.Venue!.Name))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

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

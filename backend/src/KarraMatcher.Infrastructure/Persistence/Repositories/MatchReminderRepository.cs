using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Matches;
using KarraMatcher.Domain.Matches;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

internal sealed class MatchReminderRepository(KarraMatcherDbContext context)
    : IMatchReminderRepository
{
    public async Task<IReadOnlyList<DueMatch>> ListDueAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken) =>
        await context.Matches
            .AsNoTracking()
            .Where(m => m.KickoffUtc >= fromUtc
                && m.KickoffUtc < toUtc
                && m.Status != MatchStatus.Cancelled
                && m.ReminderSentUtc == null)
            .OrderBy(m => m.KickoffUtc)
            .Select(m => new DueMatch(
                m.Id, m.TeamId, m.KickoffUtc, m.OpponentName, m.IsHome, m.Venue!.Name))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task MarkRemindedAsync(
        IReadOnlyCollection<Guid> matchIds,
        DateTime sentUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(matchIds);

        if (matchIds.Count == 0)
        {
            return;
        }

        var reminded = await context.Matches
            .Where(m => matchIds.Contains(m.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var match in reminded)
        {
            match.ReminderSentUtc = sentUtc;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

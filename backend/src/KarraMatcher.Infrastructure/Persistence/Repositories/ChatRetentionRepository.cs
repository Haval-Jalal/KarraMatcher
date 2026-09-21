using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Chat;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

internal sealed class ChatRetentionRepository(KarraMatcherDbContext context)
    : IChatRetentionRepository
{
    public async Task<int> PurgeOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken)
    {
        // Bara id:n — meddelandetexten läses aldrig in i minnet (§KM.10).
        var messageIds = await context.ChatMessages
            .AsNoTracking()
            .Where(m => m.PublishAtUtc < cutoffUtc)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (messageIds.Count == 0)
        {
            return 0;
        }

        // Anmälningar först (barn), sedan meddelanden (förälder) — FK-ordning. Stub-entiteter,
        // så ingenting laddas in.
        var reportIds = await context.ChatReports
            .AsNoTracking()
            .Where(r => messageIds.Contains(r.MessageId))
            .Select(r => r.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        context.ChatReports.RemoveRange(reportIds.Select(id => new ChatReport { Id = id }));
        context.ChatMessages.RemoveRange(messageIds.Select(id => new ChatMessage { Id = id }));

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return messageIds.Count;
    }
}

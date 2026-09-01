using KarraMatcher.Application.Abstractions.Audit;
using KarraMatcher.Domain.Audit;

namespace KarraMatcher.Infrastructure.Persistence;

internal sealed class AuditLog(KarraMatcherDbContext context, TimeProvider clock) : IAuditLog
{
    public async Task RecordAsync(
        string action,
        Guid actorAccountId,
        CancellationToken cancellationToken) =>
        await context.AuditEntries.AddAsync(
            new AuditEntry
            {
                Id = Guid.NewGuid(),
                Action = action,
                ActorAccountId = actorAccountId,
                OccurredUtc = clock.GetUtcNow().UtcDateTime,
            },
            cancellationToken).ConfigureAwait(false);
}

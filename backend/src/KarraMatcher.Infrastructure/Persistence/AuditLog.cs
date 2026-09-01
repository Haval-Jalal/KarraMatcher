using KarraMatcher.Application.Abstractions.Audit;
using KarraMatcher.Domain.Audit;

namespace KarraMatcher.Infrastructure.Persistence;

internal sealed class AuditLog(KarraMatcherDbContext context, TimeProvider clock) : IAuditLog
{
    public async Task RecordAsync(
        string action,
        Guid actorAccountId,
        CancellationToken cancellationToken,
        Guid? subjectId = null,
        string? details = null) =>
        await context.AuditEntries.AddAsync(
            new AuditEntry
            {
                Id = Guid.NewGuid(),
                Action = action,
                ActorAccountId = actorAccountId,
                SubjectId = subjectId,
                Details = details,
                OccurredUtc = clock.GetUtcNow().UtcDateTime,
            },
            cancellationToken).ConfigureAwait(false);
}

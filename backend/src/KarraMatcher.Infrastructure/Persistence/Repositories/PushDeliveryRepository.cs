using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Abstractions.Push;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

internal sealed class PushDeliveryRepository(KarraMatcherDbContext context, TimeProvider clock)
    : IPushDeliveryRepository
{
    public async Task<IReadOnlyList<PushTarget>> ListForTeamAsync(
        Guid teamId,
        CancellationToken cancellationToken) =>
        await context.PushSubscriptions
            .AsNoTracking()
            .Where(s => s.TeamId == teamId)
            .Select(s => new PushTarget(s.Id, s.Endpoint, s.P256dh, s.Auth))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<PushTarget>> ListForAccountsAsync(
        IReadOnlyCollection<Guid> accountIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountIds);

        if (accountIds.Count == 0)
        {
            return [];
        }

        return await context.PushSubscriptions
            .AsNoTracking()
            .Where(s => s.AccountId != null && accountIds.Contains(s.AccountId.Value))
            .Select(s => new PushTarget(s.Id, s.Endpoint, s.P256dh, s.Auth))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task RemoveAsync(
        IReadOnlyCollection<Guid> subscriptionIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subscriptionIds);

        if (subscriptionIds.Count == 0)
        {
            return;
        }

        var doomed = await context.PushSubscriptions
            .Where(s => subscriptionIds.Contains(s.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        context.PushSubscriptions.RemoveRange(doomed);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkDeliveredAsync(
        IReadOnlyCollection<Guid> subscriptionIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subscriptionIds);

        if (subscriptionIds.Count == 0)
        {
            return;
        }

        var now = clock.GetUtcNow().UtcDateTime;

        var delivered = await context.PushSubscriptions
            .Where(s => subscriptionIds.Contains(s.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var subscription in delivered)
        {
            subscription.LastUsedUtc = now;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

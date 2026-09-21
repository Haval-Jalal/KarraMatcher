using System.Linq.Expressions;

using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Abstractions.Push;
using KarraMatcher.Application.Features.Push;
using KarraMatcher.Domain.Push;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

internal sealed class PushDeliveryRepository(
    KarraMatcherDbContext context,
    IMembershipService membership,
    TimeProvider clock)
    : IPushDeliveryRepository
{
    public async Task<IReadOnlyList<PushTarget>> ListForTeamAsync(
        Guid teamId,
        PushCategory category,
        CancellationToken cancellationToken)
    {
        // Stangd app (§KM.3, `#200`): en lag-notis gar bara till lagets *medlemmar*, inte till
        // vem som helst som prenumererar. En anonym prenumeration (utan konto) nas aldrig.
        var members = (await membership.MemberAccountIdsAsync(teamId, cancellationToken)
            .ConfigureAwait(false)).ToHashSet();

        if (members.Count == 0)
        {
            return [];
        }

        // Medlemmar som stangt av kategorin for laget. Franvaro av rad = allt pa.
        var disabled = DisabledAccountIds(teamId, category);

        return await context.PushSubscriptions
            .AsNoTracking()
            .Where(s => s.AccountId != null
                && members.Contains(s.AccountId.Value)
                && !disabled.Contains(s.AccountId.Value))
            .Select(s => new PushTarget(s.Id, s.Endpoint, s.P256dh, s.Auth))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PushTarget>> ListForAccountsAsync(
        Guid teamId,
        IReadOnlyCollection<Guid> accountIds,
        PushCategory category,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountIds);

        if (accountIds.Count == 0)
        {
            return [];
        }

        // Aven en kontoriktad notis gar mot ett lag: den som stangt av kategorin for det
        // laget ska inte nas, ens for sin egen samakning.
        var disabled = DisabledAccountIds(teamId, category);

        return await context.PushSubscriptions
            .AsNoTracking()
            .Where(s => s.AccountId != null
                && accountIds.Contains(s.AccountId.Value)
                && !disabled.Contains(s.AccountId.Value))
            .Select(s => new PushTarget(s.Id, s.Endpoint, s.P256dh, s.Auth))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Kontona som stängt av kategorin för laget. Frånvaro av en rad = allt på, så bara den
    /// som uttryckligen satt kolumnen till falskt hamnar här.
    /// </summary>
    private IQueryable<Guid> DisabledAccountIds(Guid teamId, PushCategory category) =>
        context.NotificationPreferences
            .Where(p => p.TeamId == teamId)
            .Where(IsDisabled(category))
            .Select(p => p.AccountId);

    private static Expression<Func<NotificationPreference, bool>> IsDisabled(PushCategory category) =>
        category switch
        {
            PushCategory.EventChange => p => !p.EventChanges,
            PushCategory.Kallelse => p => !p.Kallelser,
            PushCategory.Carpool => p => !p.Carpool,
            PushCategory.Chat => p => !p.Chat,
            _ => p => false,
        };

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

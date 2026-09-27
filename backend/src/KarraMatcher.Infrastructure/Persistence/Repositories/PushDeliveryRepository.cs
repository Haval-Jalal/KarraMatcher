using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Abstractions.Push;
using KarraMatcher.Application.Features.Push;

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

        // En enda global på/av per konto: den som stängt av notiser får ingen push. Kategorin
        // styr inte längre push — den avgör bara om mejl-fallbacken räknar beskedet som kritiskt.
        var off = AccountsWithNotificationsOff();

        return await context.PushSubscriptions
            .AsNoTracking()
            .Where(s => s.AccountId != null
                && members.Contains(s.AccountId.Value)
                && !off.Contains(s.AccountId.Value))
            .Select(s => new PushTarget(s.Id, s.Endpoint, s.P256dh, s.Auth))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PushTarget>> ListForTruppAsync(
        Guid ageGroupId,
        PushCategory category,
        CancellationToken cancellationToken)
    {
        // Trupp-vid händelse (`#332`): notisen når hela truppens medlemmar.
        var members = (await membership.MemberAccountIdsForTruppAsync(ageGroupId, cancellationToken)
            .ConfigureAwait(false)).ToHashSet();

        if (members.Count == 0)
        {
            return [];
        }

        var off = AccountsWithNotificationsOff();

        return await context.PushSubscriptions
            .AsNoTracking()
            .Where(s => s.AccountId != null
                && members.Contains(s.AccountId.Value)
                && !off.Contains(s.AccountId.Value))
            .Select(s => new PushTarget(s.Id, s.Endpoint, s.P256dh, s.Auth))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PushTarget>> ListForAccountsAsync(
        IReadOnlyCollection<Guid> accountIds,
        PushCategory category,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountIds);

        if (accountIds.Count == 0)
        {
            return [];
        }

        // Global på/av per konto — se ListForTeamAsync. Kategorin styr inte längre.
        var off = AccountsWithNotificationsOff();

        return await context.PushSubscriptions
            .AsNoTracking()
            .Where(s => s.AccountId != null
                && accountIds.Contains(s.AccountId.Value)
                && !off.Contains(s.AccountId.Value))
            .Select(s => new PushTarget(s.Id, s.Endpoint, s.P256dh, s.Auth))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Kontona som stängt av notiser helt (global på/av). Push når dem inte.</summary>
    private IQueryable<Guid> AccountsWithNotificationsOff() =>
        context.Accounts.Where(a => !a.NotificationsEnabled).Select(a => a.Id);

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

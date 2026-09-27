using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Push;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

/// <summary>
/// Mottagarna för e-postfallbacken. Komplementet till <see cref="PushDeliveryRepository"/>:
/// samma medlems- och preferensfilter, men de som <em>saknar</em> en push-prenumeration.
/// </summary>
internal sealed class EmailFallbackRepository(
    KarraMatcherDbContext context,
    IMembershipService membership) : IEmailFallbackRepository
{
    public async Task<IReadOnlyList<EmailRecipient>> ListForTeamAsync(
        Guid teamId,
        PushCategory category,
        CancellationToken cancellationToken)
    {
        var members = await membership.MemberAccountIdsAsync(teamId, cancellationToken)
            .ConfigureAwait(false);

        return await ResolveAsync(teamId, members, category, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<EmailRecipient>> ListForAccountsAsync(
        Guid teamId,
        IReadOnlyCollection<Guid> accountIds,
        PushCategory category,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountIds);

        return await ResolveAsync(teamId, accountIds, category, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<EmailRecipient>> ResolveAsync(
        Guid teamId,
        IReadOnlyCollection<Guid> candidates,
        PushCategory category,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        // Kontona som har minst en push-enhet — de kan nås av push.
        var withPush = context.PushSubscriptions
            .Where(s => s.AccountId != null)
            .Select(s => s.AccountId!.Value);

        // Mejlet är säkerhetsnätet för kritiska besked (notifiern har redan sållat på "kritisk").
        // Det går till alla i kretsen som *inte* nås av push: de utan enhet, och de som stängt
        // av notiser globalt — så "man missar inget" även om man slår av push (`#332`-uppföljning).
        return await context.Accounts
            .AsNoTracking()
            .Where(a => candidates.Contains(a.Id)
                && !(a.NotificationsEnabled && withPush.Contains(a.Id)))
            .Select(a => new EmailRecipient(a.Id, a.Email))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

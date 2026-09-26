using System.Linq.Expressions;

using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Push;
using KarraMatcher.Domain.Push;

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

        // Kontona som stängt av kategorin för laget (frånvaro av rad = allt på).
        var disabled = context.NotificationPreferences
            .Where(p => p.TeamId == teamId)
            .Where(IsDisabled(category))
            .Select(p => p.AccountId);

        // Kontona som har minst en push-enhet — de nås av push och ska inte mejlas.
        var withPush = context.PushSubscriptions
            .Where(s => s.AccountId != null)
            .Select(s => s.AccountId!.Value);

        return await context.Accounts
            .AsNoTracking()
            .Where(a => candidates.Contains(a.Id)
                && !disabled.Contains(a.Id)
                && !withPush.Contains(a.Id))
            .Select(a => new EmailRecipient(a.Id, a.Email))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static Expression<Func<NotificationPreference, bool>> IsDisabled(PushCategory category) =>
        category switch
        {
            PushCategory.EventChange => p => !p.EventChanges,
            PushCategory.Kallelse => p => !p.Kallelser,
            PushCategory.Carpool => p => !p.Carpool,
            PushCategory.Chat => p => !p.Chat,
            _ => p => false,
        };
}

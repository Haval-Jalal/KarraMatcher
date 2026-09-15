using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Push;
using KarraMatcher.Domain.Push;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

internal sealed class PushSubscriptionRepository(KarraMatcherDbContext context, TimeProvider clock)
    : IPushSubscriptionRepository
{
    public async Task<bool> SubscribeAsync(
        string slug,
        PushSubscriptionDraft draft,
        Guid? accountId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(slug);
        ArgumentNullException.ThrowIfNull(draft);

        var teamId = await context.Teams
            .AsNoTracking()
            .Where(team => team.Slug == slug)
            .Select(team => (Guid?)team.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (teamId is null)
        {
            return false;
        }

        var existing = await context.PushSubscriptions
            .FirstOrDefaultAsync(
                s => s.TeamId == teamId.Value && s.Endpoint == draft.Endpoint,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            /*
             * Samma webblasare igen. Nycklarna skrivs over i stallet for att lamnas: en
             * webblasare far nya nycklar nar prenumerationen fornyas, och en rad med gamla
             * nycklar ar en rad vars notiser inte gar att kryptera.
             */
            existing.P256dh = draft.P256dh;
            existing.Auth = draft.Auth;

            /*
             * Kopplingen till kontot uppdateras ocksa (#63): en gast som prenumererade och
             * sedan loggat in ska bli natbar for sina samakningsnotiser. En som loggat ut ger
             * null och kopplingen slapper -- prenumerationen blir anonym igen, men bor kvar.
             */
            existing.AccountId = accountId;
        }
        else
        {
            await context.PushSubscriptions.AddAsync(
                new PushSubscription
                {
                    Id = Guid.NewGuid(),
                    TeamId = teamId.Value,
                    AccountId = accountId,
                    Endpoint = draft.Endpoint,
                    P256dh = draft.P256dh,
                    Auth = draft.Auth,
                    CreatedUtc = clock.GetUtcNow().UtcDateTime,
                },
                cancellationToken).ConfigureAwait(false);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    public async Task<bool> UnsubscribeAsync(
        string slug,
        string endpoint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(slug);
        ArgumentNullException.ThrowIfNull(endpoint);

        var found = await context.PushSubscriptions
            .Where(s => s.Endpoint == endpoint && context.Teams
                .Any(team => team.Id == s.TeamId && team.Slug == slug))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (found.Count == 0)
        {
            return false;
        }

        context.PushSubscriptions.RemoveRange(found);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }
}

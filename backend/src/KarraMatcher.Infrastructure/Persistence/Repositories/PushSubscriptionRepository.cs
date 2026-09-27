using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Push;
using KarraMatcher.Domain.Push;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

internal sealed class PushSubscriptionRepository(KarraMatcherDbContext context, TimeProvider clock)
    : IPushSubscriptionRepository
{
    public async Task SubscribeAsync(
        PushSubscriptionDraft draft,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        // En rad per webblasare (`#332`-uppfoljning): adressen ar enhetens identitet. Finns den
        // redan uppdateras nycklarna och kontot i stallet for att en andra rad laggs till -- annars
        // fick samma enhet en notis per rad.
        var existing = await context.PushSubscriptions
            .FirstOrDefaultAsync(s => s.Endpoint == draft.Endpoint, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            // Nya nycklar vid fornyelse -- en rad med gamla nycklar gar inte att kryptera till.
            existing.P256dh = draft.P256dh;
            existing.Auth = draft.Auth;
            existing.AccountId = accountId;
            existing.TeamId = null;
        }
        else
        {
            await context.PushSubscriptions.AddAsync(
                new PushSubscription
                {
                    Id = Guid.NewGuid(),
                    TeamId = null,
                    AccountId = accountId,
                    Endpoint = draft.Endpoint,
                    P256dh = draft.P256dh,
                    Auth = draft.Auth,
                    CreatedUtc = clock.GetUtcNow().UtcDateTime,
                },
                cancellationToken).ConfigureAwait(false);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> UnsubscribeAsync(string endpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        var found = await context.PushSubscriptions
            .Where(s => s.Endpoint == endpoint)
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

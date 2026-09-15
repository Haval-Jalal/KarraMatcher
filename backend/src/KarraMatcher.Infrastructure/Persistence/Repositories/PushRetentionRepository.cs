using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Push;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

/// <summary>
/// Raderar tysta push-prenumerationer (`#68`, säkerhetschecklistan 9.8).
///
/// <h3>Bara id:n läses hem</h3>
///
/// <para>
/// Raderingen frågar efter nycklar och tar bort raderna genom att fästa tomma stubbar med
/// rätt id. Endpointen och krypteringsnycklarna — som är secrets (§KM.10) — hämtas alltså
/// aldrig till minnet för att kunna raderas.
/// </para>
///
/// <h3>Tyst mäts på två fält</h3>
///
/// <para>
/// <c>LastUsedUtc</c> sätts först vid en lyckad leverans. En prenumeration som aldrig fått
/// ett utskick har det som null, och faller då tillbaka på <c>CreatedUtc</c> — annars hade
/// den aldrig gallrats.
/// </para>
/// </summary>
internal sealed class PushRetentionRepository(KarraMatcherDbContext context)
    : IPushRetentionRepository
{
    public async Task<int> PurgeInactiveAsync(
        DateTime cutoffUtc,
        CancellationToken cancellationToken)
    {
        var doomed = await context.PushSubscriptions
            .AsNoTracking()
            .Where(subscription => (subscription.LastUsedUtc ?? subscription.CreatedUtc) < cutoffUtc)
            .Select(subscription => subscription.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (doomed.Count == 0)
        {
            return 0;
        }

        // Stubbar med bara nyckeln satt. Endpoint och nycklar ar required, men vardet spelar
        // ingen roll for en radering -- tomma varden ar det narmaste vi kommer att inte rora
        // secreten (§KM.10).
        context.PushSubscriptions.RemoveRange(
            doomed.Select(id => new PushSubscription
            {
                Id = id,
                Endpoint = string.Empty,
                P256dh = string.Empty,
                Auth = string.Empty,
            }));

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return doomed.Count;
    }
}

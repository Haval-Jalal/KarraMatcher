using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Carpool;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

/// <summary>
/// Raderar samåkning för spelade matcher (§KM.12, säkerhetschecklistan 9.8).
///
/// <h3>Bara id:n läses hem</h3>
///
/// <para>
/// Raderingen frågar efter nycklar och tar bort raderna genom att fästa tomma stubbar med
/// rätt id. Föräldrarnas notiser och hälsningar hämtas alltså aldrig till minnet — en
/// gallring som läser in fritexten för att kunna radera den har missförstått sin uppgift.
/// </para>
///
/// <h3>Ordningen är inte kosmetisk</h3>
///
/// <para>
/// Förfrågningarna först, erbjudandena sedan. En förfrågan pekar på ett erbjudande, och
/// tvärtom hade främmandenyckeln stoppat raderingen.
/// </para>
/// </summary>
internal sealed class CarpoolRetentionRepository(KarraMatcherDbContext context)
    : ICarpoolRetentionRepository
{
    public async Task<CarpoolPurgeResult> PurgeAsync(
        DateTime cutoffUtc,
        CancellationToken cancellationToken)
    {
        /*
         * Matcherna som passerat gransen tas ut for sig, sa att bada raderingarna utgar
         * fran exakt samma uppsattning. Ett underfragefilter hade racknats om per sats, och
         * en match som hann passera gransen daremellan kunde lamna kvar ett erbjudande vars
         * forfragningar redan var borta.
         */
        var expired = await context.Events
            .AsNoTracking()
            .Where(match => match.KickoffUtc < cutoffUtc)
            .Select(match => match.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (expired.Count == 0)
        {
            return CarpoolPurgeResult.Nothing;
        }

        var offerIds = await context.CarpoolOffers
            .AsNoTracking()
            .Where(offer => expired.Contains(offer.MatchId))
            .Select(offer => offer.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (offerIds.Count == 0)
        {
            return CarpoolPurgeResult.Nothing;
        }

        var requestIds = await context.CarpoolRequests
            .AsNoTracking()
            .Where(request => offerIds.Contains(request.OfferId))
            .Select(request => request.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Stubbar med bara nyckeln satt. De ovriga faltens varden spelar ingen roll for en
        // radering, och tomma varden ar det narmaste vi kommer att inte rora innehallet.
        context.CarpoolRequests.RemoveRange(
            requestIds.Select(id => new CarpoolRequest { Id = id }));

        context.CarpoolOffers.RemoveRange(
            offerIds.Select(id => new CarpoolOffer { Id = id, DeparturePlace = string.Empty }));

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new CarpoolPurgeResult(requestIds.Count, offerIds.Count);
    }
}

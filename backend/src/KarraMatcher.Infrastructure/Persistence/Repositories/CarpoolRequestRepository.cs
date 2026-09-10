using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Carpool;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

internal sealed class CarpoolRequestRepository(KarraMatcherDbContext context)
    : ICarpoolRequestRepository
{
    public Task<CarpoolRequest?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        context.CarpoolRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<bool> HasActiveAsync(
        Guid offerId,
        Guid requesterAccountId,
        CancellationToken cancellationToken) =>
        context.CarpoolRequests
            .AsNoTracking()
            .AnyAsync(
                r => r.OfferId == offerId
                    && r.RequesterAccountId == requesterAccountId
                    && (r.Status == CarpoolRequestStatus.Pending
                        || r.Status == CarpoolRequestStatus.Accepted),
                cancellationToken);

    public async Task<IReadOnlyList<CarpoolRequest>> ListForOfferAsync(
        Guid offerId,
        CancellationToken cancellationToken) =>
        await context.CarpoolRequests
            .AsNoTracking()
            .Where(r => r.OfferId == offerId)
            .OrderBy(r => r.CreatedUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<int> AcceptedSeatsAsync(Guid offerId, CancellationToken cancellationToken) =>
        await context.CarpoolRequests
            .AsNoTracking()
            .Where(r => r.OfferId == offerId && r.Status == CarpoolRequestStatus.Accepted)
            .SumAsync(r => r.Seats, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyDictionary<Guid, int>> AcceptedSeatsForOffersAsync(
        IReadOnlyCollection<Guid> offerIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(offerIds);

        if (offerIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        /*
         * Grupperat i databasen. Att hamta raderna och summera i minnet hade fungerat lika
         * bra pa en match med fyra erbjudanden, och lika daligt den dag det ar femtio.
         */
        var rows = await context.CarpoolRequests
            .AsNoTracking()
            .Where(r => offerIds.Contains(r.OfferId) && r.Status == CarpoolRequestStatus.Accepted)
            .GroupBy(r => r.OfferId)
            .Select(group => new { OfferId = group.Key, Seats = group.Sum(r => r.Seats) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ToDictionary(row => row.OfferId, row => row.Seats);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountPendingForOffersAsync(
        IReadOnlyCollection<Guid> offerIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(offerIds);

        if (offerIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        // Bara antalet. Halsningen ar fritext och har ingenting i en overblick att gora.
        var rows = await context.CarpoolRequests
            .AsNoTracking()
            .Where(r => offerIds.Contains(r.OfferId) && r.Status == CarpoolRequestStatus.Pending)
            .GroupBy(r => r.OfferId)
            .Select(group => new { OfferId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ToDictionary(row => row.OfferId, row => row.Count);
    }

    public async Task AddAsync(CarpoolRequest request, CancellationToken cancellationToken) =>
        await context.CarpoolRequests.AddAsync(request, cancellationToken).ConfigureAwait(false);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}

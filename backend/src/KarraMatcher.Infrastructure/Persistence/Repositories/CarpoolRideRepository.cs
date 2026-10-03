using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Carpool;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

internal sealed class CarpoolRideRepository(KarraMatcherDbContext context)
    : ICarpoolRideRepository
{
    public async Task AddRequestAsync(
        CarpoolRideRequest request, CancellationToken cancellationToken) =>
        await context.CarpoolRideRequests.AddAsync(request, cancellationToken).ConfigureAwait(false);

    public Task<CarpoolRideRequest?> FindRequestForUpdateAsync(
        Guid id, CancellationToken cancellationToken) =>
        context.CarpoolRideRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<CarpoolRideRequest>> ListOpenForMatchAsync(
        Guid matchId, CancellationToken cancellationToken) =>
        await context.CarpoolRideRequests
            .AsNoTracking()
            .Where(r => r.MatchId == matchId && r.Status == CarpoolRideRequestStatus.Open)
            .OrderBy(r => r.CreatedUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AddOfferAsync(
        CarpoolRideOffer offer, CancellationToken cancellationToken) =>
        await context.CarpoolRideOffers.AddAsync(offer, cancellationToken).ConfigureAwait(false);

    public Task<CarpoolRideOffer?> FindOfferForUpdateAsync(
        Guid id, CancellationToken cancellationToken) =>
        context.CarpoolRideOffers.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public Task<bool> HasActiveOfferAsync(
        Guid rideRequestId,
        Guid driverAccountId,
        CancellationToken cancellationToken) =>
        context.CarpoolRideOffers
            .AsNoTracking()
            .AnyAsync(
                o => o.RideRequestId == rideRequestId
                    && o.DriverAccountId == driverAccountId
                    && (o.Status == CarpoolRequestStatus.Pending
                        || o.Status == CarpoolRequestStatus.Accepted),
                cancellationToken);

    public async Task<IReadOnlyList<CarpoolRideOffer>> ListOffersForRequestAsync(
        Guid rideRequestId,
        CancellationToken cancellationToken) =>
        await context.CarpoolRideOffers
            .AsNoTracking()
            .Where(o => o.RideRequestId == rideRequestId)
            .OrderBy(o => o.CreatedUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}

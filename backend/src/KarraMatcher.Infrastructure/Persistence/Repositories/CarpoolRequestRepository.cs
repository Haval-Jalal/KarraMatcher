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

    public async Task AddAsync(CarpoolRequest request, CancellationToken cancellationToken) =>
        await context.CarpoolRequests.AddAsync(request, cancellationToken).ConfigureAwait(false);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}

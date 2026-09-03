using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Carpool;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

internal sealed class CarpoolOfferRepository(KarraMatcherDbContext context) : ICarpoolOfferRepository
{
    public Task<bool> MatchExistsAsync(Guid matchId, CancellationToken cancellationToken) =>
        context.Matches.AsNoTracking().AnyAsync(m => m.Id == matchId, cancellationToken);

    /// <summary>Spårad — det här är skrivvägen.</summary>
    public Task<CarpoolOffer?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        context.CarpoolOffers.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task<IReadOnlyList<CarpoolOffer>> ListOpenForMatchAsync(
        Guid matchId,
        CancellationToken cancellationToken) =>
        await context.CarpoolOffers
            .AsNoTracking()
            .Where(o => o.MatchId == matchId && o.Status == CarpoolOfferStatus.Open)
            .OrderBy(o => o.DepartureUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AddAsync(CarpoolOffer offer, CancellationToken cancellationToken) =>
        await context.CarpoolOffers.AddAsync(offer, cancellationToken).ConfigureAwait(false);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}

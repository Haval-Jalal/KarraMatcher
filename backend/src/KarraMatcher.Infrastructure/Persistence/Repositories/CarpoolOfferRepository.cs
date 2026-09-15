using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Carpool;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

internal sealed class CarpoolOfferRepository(KarraMatcherDbContext context) : ICarpoolOfferRepository
{
    public Task<bool> MatchExistsAsync(Guid matchId, CancellationToken cancellationToken) =>
        context.Matches.AsNoTracking().AnyAsync(m => m.Id == matchId, cancellationToken);

    public async Task<Guid?> FindMatchTeamIdAsync(Guid matchId, CancellationToken cancellationToken) =>
        await context.Matches
            .AsNoTracking()
            .Where(m => m.Id == matchId)
            .Select(m => (Guid?)m.TeamId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

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

    public async Task<IReadOnlyList<CarpoolOffer>> ListOpenForMatchesAsync(
        IReadOnlyCollection<Guid> matchIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(matchIds);

        if (matchIds.Count == 0)
        {
            return [];
        }

        return await context.CarpoolOffers
            .AsNoTracking()
            .Where(o => matchIds.Contains(o.MatchId) && o.Status == CarpoolOfferStatus.Open)
            .OrderBy(o => o.DepartureUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(CarpoolOffer offer, CancellationToken cancellationToken) =>
        await context.CarpoolOffers.AddAsync(offer, cancellationToken).ConfigureAwait(false);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}

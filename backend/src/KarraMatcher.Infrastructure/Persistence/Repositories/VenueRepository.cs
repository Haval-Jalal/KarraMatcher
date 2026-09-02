using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Venues;
using KarraMatcher.Domain.Matches;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

internal sealed class VenueRepository(KarraMatcherDbContext context) : IVenueRepository
{
    /// <summary>Så många förslag som får plats utan att listan blir en lista att läsa.</summary>
    private const int MaxSuggestions = 8;

    public async Task<IReadOnlyList<VenueDto>> SearchAsync(
        string term,
        CancellationToken cancellationToken)
    {
        var query = context.Venues.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(term))
        {
            // EF.Functions.Like gar till databasen och ar skiftlagesokanslig i Postgres
            // via ILike. Att filtrera i minnet hade hamtat hela tabellen for varje
            // tangenttryckning.
            var pattern = $"%{term}%";

            query = query.Where(v =>
                EF.Functions.Like(v.Name, pattern) || EF.Functions.Like(v.Address, pattern));
        }

        return await query
            .OrderByDescending(v => v.IsHome)
            .ThenBy(v => v.Name)
            .Take(MaxSuggestions)
            .Select(v => new VenueDto(v.Id, v.Name, v.Address, v.Latitude, v.Longitude, v.IsHome))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken) =>
        context.Venues.AsNoTracking().AnyAsync(v => v.Name == name, cancellationToken);

    public async Task AddAsync(Venue venue, CancellationToken cancellationToken) =>
        await context.Venues.AddAsync(venue, cancellationToken).ConfigureAwait(false);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}

using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Matches;
using KarraMatcher.Domain.Teams;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

internal sealed class MatchAdminRepository(KarraMatcherDbContext context) : IMatchAdminRepository
{
    /// <summary>
    /// Spårad, till skillnad från läsvägarna som kör <c>AsNoTracking</c>. Laget läses in
    /// eftersom behörigheten prövas mot dess slug.
    /// </summary>
    public Task<Match?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        context.Matches
            .Include(m => m.Team)
            .Include(m => m.Venue)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public Task<Team?> FindTeamBySlugAsync(string slug, CancellationToken cancellationToken) =>
        context.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == slug, cancellationToken);

    public Task<bool> VenueExistsAsync(Guid venueId, CancellationToken cancellationToken) =>
        context.Venues.AsNoTracking().AnyAsync(v => v.Id == venueId, cancellationToken);

    public async Task AddAsync(Match match, CancellationToken cancellationToken) =>
        await context.Matches.AddAsync(match, cancellationToken).ConfigureAwait(false);

    public void Remove(Match match) => context.Matches.Remove(match);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}

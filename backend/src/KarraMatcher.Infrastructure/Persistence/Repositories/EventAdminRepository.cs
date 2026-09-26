using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Events;
using KarraMatcher.Domain.Teams;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

internal sealed class EventAdminRepository(KarraMatcherDbContext context) : IEventAdminRepository
{
    /// <summary>
    /// Spårad, till skillnad från läsvägarna som kör <c>AsNoTracking</c>. Laget läses in
    /// eftersom behörigheten prövas mot dess slug.
    /// </summary>
    public Task<Event?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        context.Events
            .Include(e => e.Team)
                .ThenInclude(team => team!.AgeGroup)
                    .ThenInclude(ageGroup => ageGroup!.Club)
            .Include(e => e.Venue)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public Task<Team?> FindTeamBySlugAsync(string slug, CancellationToken cancellationToken) =>
        context.Teams
            .AsNoTracking()
            .Include(t => t.AgeGroup)
                .ThenInclude(ageGroup => ageGroup!.Club)
            .FirstOrDefaultAsync(t => t.Slug == slug, cancellationToken);

    public Task<bool> VenueExistsAsync(Guid venueId, CancellationToken cancellationToken) =>
        context.Venues.AsNoTracking().AnyAsync(v => v.Id == venueId, cancellationToken);

    public async Task AddAsync(Event item, CancellationToken cancellationToken) =>
        await context.Events.AddAsync(item, cancellationToken).ConfigureAwait(false);

    public void Remove(Event item) => context.Events.Remove(item);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}

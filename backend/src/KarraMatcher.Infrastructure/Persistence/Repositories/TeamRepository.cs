using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Events;
using KarraMatcher.Domain.Teams;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core-implementationen av <see cref="ITeamRepository"/>.
///
/// <para>
/// Allt här är läsning, så allt körs med <c>AsNoTracking</c> — change tracking kostar minne
/// och tid utan att tillföra något när ingenting ska sparas.
/// </para>
/// </summary>
internal sealed class TeamRepository(KarraMatcherDbContext context) : ITeamRepository
{
    public async Task<IReadOnlyList<Team>> GetAllAsync(CancellationToken cancellationToken) =>
        await context.Teams
            .AsNoTracking()
            .Include(team => team.AgeGroup)
            .OrderBy(team => team.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<Team?> FindBySlugAsync(string slug, CancellationToken cancellationToken) =>
        await context.Teams
            .AsNoTracking()
            .Include(team => team.AgeGroup)
            .FirstOrDefaultAsync(team => team.Slug == slug, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Event>> GetEventsAsync(
        Guid teamId,
        CancellationToken cancellationToken) =>
        await context.Events
            .AsNoTracking()
            .Include(item => item.Venue)
            .Where(item => item.TeamId == teamId)

            // Sorteringen sker i databasen och inte i minnet. Ordningen är en del av
            // kontraktet -- appen visar händelserna i tidsordning och sorterar inte om.
            .OrderBy(item => item.KickoffUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}

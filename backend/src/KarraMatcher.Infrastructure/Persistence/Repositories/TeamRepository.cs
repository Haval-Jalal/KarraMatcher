using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Matches;
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

    public async Task<IReadOnlyList<Match>> GetMatchesAsync(
        Guid teamId,
        CancellationToken cancellationToken) =>
        await context.Matches
            .AsNoTracking()
            .Include(match => match.Venue)
            .Where(match => match.TeamId == teamId)

            // Sorteringen sker i databasen och inte i minnet. Ordningen är en del av
            // kontraktet -- appen visar matcherna i tidsordning och sorterar inte om.
            .OrderBy(match => match.KickoffUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}

using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Matches;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

/// <summary>EF Core-implementationen av <see cref="IMatchRepository"/>. Endast läsning.</summary>
internal sealed class MatchRepository(KarraMatcherDbContext context) : IMatchRepository
{
    public async Task<Match?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await context.Matches
            .AsNoTracking()
            .Include(match => match.Venue)

            // Laget behövs för lagfärgen och för vägen tillbaka till schemat, och
            // åldersgruppen för rubriken. Att läsa in dem här sparar två anrop från
            // klienten på ett nät som ofta är dåligt.
            .Include(match => match.Team)
                .ThenInclude(team => team!.AgeGroup)
            .FirstOrDefaultAsync(match => match.Id == id, cancellationToken)
            .ConfigureAwait(false);
}

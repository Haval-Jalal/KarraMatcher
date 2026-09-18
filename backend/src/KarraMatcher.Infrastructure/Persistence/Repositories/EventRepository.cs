using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Events;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

/// <summary>EF Core-implementationen av <see cref="IEventRepository"/>. Endast läsning.</summary>
internal sealed class EventRepository(KarraMatcherDbContext context) : IEventRepository
{
    public async Task<Event?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await context.Events
            .AsNoTracking()
            .Include(item => item.Venue)

            // Laget behövs för lagfärgen och för vägen tillbaka till schemat, och
            // åldersgruppen för rubriken. Att läsa in dem här sparar två anrop från
            // klienten på ett nät som ofta är dåligt.
            .Include(item => item.Team)
                .ThenInclude(team => team!.AgeGroup)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            .ConfigureAwait(false);
}

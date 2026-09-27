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
                    .ThenInclude(ageGroup => ageGroup!.Club)

            // En trupp-vid händelse (`#332`) saknar lag; truppen (och klubben, för en
            // hemma-händelses adress) nås då direkt via AgeGroup.
            .Include(item => item.AgeGroup)
                .ThenInclude(ageGroup => ageGroup!.Club)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Event>> ListByTruppAsync(
        Guid ageGroupId, CancellationToken cancellationToken) =>
        await context.Events
            .AsNoTracking()
            .Include(item => item.Venue)
            .Include(item => item.Team)
                .ThenInclude(team => team!.AgeGroup)
                    .ThenInclude(ageGroup => ageGroup!.Club)
            .Include(item => item.AgeGroup)
                .ThenInclude(ageGroup => ageGroup!.Club)

            // Hela truppen: både lag-riktade och trupp-vida händelser hör hit via AgeGroupId.
            .Where(item => item.AgeGroupId == ageGroupId)
            .OrderBy(item => item.KickoffUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}

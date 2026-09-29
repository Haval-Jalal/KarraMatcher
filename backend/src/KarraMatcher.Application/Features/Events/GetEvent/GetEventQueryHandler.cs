using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Teams;

namespace KarraMatcher.Application.Features.Events.GetEvent;

internal sealed class GetEventQueryHandler(IEventRepository events)
    : IQueryHandler<GetEventQuery, EventDetailDto?>
{
    public async Task<EventDetailDto?> HandleAsync(
        GetEventQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var item = await events.FindByIdAsync(query.Id, cancellationToken).ConfigureAwait(false);

        if (item is null)
        {
            return null;
        }

        // Laget är valfritt (`#332`): en trupp-vid händelse har inget lag (och ingen lagfärg),
        // men hör alltid till en trupp. Truppens id och namn tas från händelsen direkt (AgeGroup),
        // inte via laget, så de finns även utan lag — detaljsidan får då en rubrik ändå (#408).
        var truppName = item.Team?.AgeGroup?.Name ?? item.AgeGroup?.Name ?? "Truppen";

        return new EventDetailDto(item.ToDto(), item.Team?.ToDto(), item.AgeGroupId, truppName);
    }
}

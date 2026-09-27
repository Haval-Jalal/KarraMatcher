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
        // men hör alltid till en trupp. Truppens id tas från händelsen direkt (AgeGroupId),
        // inte via laget, så det finns även utan lag.
        return new EventDetailDto(item.ToDto(), item.Team?.ToDto(), item.AgeGroupId);
    }
}

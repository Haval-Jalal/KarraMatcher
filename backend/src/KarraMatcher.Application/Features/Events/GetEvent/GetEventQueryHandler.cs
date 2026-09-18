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

        if (item?.Team is null)
        {
            // Utan lag går händelsen inte att visa: sidan behöver lagfärgen och vägen
            // tillbaka till schemat. Det ska inte kunna hända -- främmande nyckeln är
            // obligatorisk -- men ett null här vore ett 500 hos anroparen.
            return null;
        }

        return new EventDetailDto(item.ToDto(), item.Team.ToDto());
    }
}

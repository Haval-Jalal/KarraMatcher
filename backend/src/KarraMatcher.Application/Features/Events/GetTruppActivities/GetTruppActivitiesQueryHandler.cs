using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Teams;

namespace KarraMatcher.Application.Features.Events.GetTruppActivities;

internal sealed class GetTruppActivitiesQueryHandler(IEventRepository events)
    : IQueryHandler<GetTruppActivitiesQuery, IReadOnlyList<TruppActivityDto>>
{
    public async Task<IReadOnlyList<TruppActivityDto>> HandleAsync(
        GetTruppActivitiesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var items = await events.ListByTruppAsync(query.TruppId, cancellationToken).ConfigureAwait(false);

        // Laget är valfritt (`#332`): en trupp-vid händelse ger Team = null (ingen lagfärg).
        return [.. items.Select(item => new TruppActivityDto(item.ToDto(), item.Team?.ToDto()))];
    }
}

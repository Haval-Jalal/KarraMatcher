using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Teams;

namespace KarraMatcher.Application.Features.Events.GetTruppActivities;

internal sealed class GetTruppActivitiesQueryHandler(
    IEventRepository events, IMembershipService membership)
    : IQueryHandler<GetTruppActivitiesQuery, IReadOnlyList<TruppActivityDto>>
{
    public async Task<IReadOnlyList<TruppActivityDto>> HandleAsync(
        GetTruppActivitiesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var items = await events.ListByTruppAsync(query.TruppId, cancellationToken).ConfigureAwait(false);

        // Gallra matcher man inte är kallad på (om man inte sköter truppen); träning/cup/övrigt är
        // kvar för alla truppens medlemmar (§KM.7, ägarbeslut).
        var visibility = await membership
            .GetMatchVisibilityAsync(query.ActorAccountId, cancellationToken)
            .ConfigureAwait(false);

        // Laget är valfritt (`#332`): en trupp-vid händelse ger Team = null (ingen lagfärg).
        return
        [
            .. items
                .Where(item => visibility.CanSee(item.Type, item.AgeGroupId, item.Id))
                .Select(item => new TruppActivityDto(item.ToDto(), item.Team?.ToDto())),
        ];
    }
}

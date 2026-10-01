using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Abstractions.Persistence;

namespace KarraMatcher.Application.Features.Teams.GetTeamEvents;

internal sealed class GetTeamEventsQueryHandler(ITeamRepository teams, IMembershipService membership)
    : IQueryHandler<GetTeamEventsQuery, TeamEventsDto?>
{
    public async Task<TeamEventsDto?> HandleAsync(
        GetTeamEventsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var team = await teams.FindBySlugAsync(query.Slug, cancellationToken).ConfigureAwait(false);

        if (team is null)
        {
            return null;
        }

        var events = await teams.GetEventsAsync(team.Id, cancellationToken).ConfigureAwait(false);

        // Gallra matcher man inte är kallad på (om man inte sköter laget/truppen) — träning/cup/
        // övrigt är kvar för alla lag-medlemmar (§KM.7, ägarbeslut).
        var visibility = await membership
            .GetMatchVisibilityAsync(query.ActorAccountId, cancellationToken)
            .ConfigureAwait(false);

        var visible = events.Where(item => visibility.CanSee(item.Type, item.AgeGroupId, item.Id));

        return new TeamEventsDto(
            team.ToDto(), [.. visible.Select(item => item.ToDto())], team.AgeGroupId);
    }
}

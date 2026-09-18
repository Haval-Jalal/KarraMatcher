using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Abstractions.Persistence;

namespace KarraMatcher.Application.Features.Teams.GetTeamEvents;

internal sealed class GetTeamEventsQueryHandler(ITeamRepository teams)
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

        return new TeamEventsDto(team.ToDto(), [.. events.Select(item => item.ToDto())]);
    }
}

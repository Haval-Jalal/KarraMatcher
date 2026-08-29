using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Abstractions.Persistence;

namespace KarraMatcher.Application.Features.Teams.GetTeams;

internal sealed class GetTeamsQueryHandler(ITeamRepository teams)
    : IQueryHandler<GetTeamsQuery, IReadOnlyList<TeamDto>>
{
    public async Task<IReadOnlyList<TeamDto>> HandleAsync(
        GetTeamsQuery query,
        CancellationToken cancellationToken)
    {
        var all = await teams.GetAllAsync(cancellationToken).ConfigureAwait(false);

        return [.. all.Select(team => team.ToDto())];
    }
}

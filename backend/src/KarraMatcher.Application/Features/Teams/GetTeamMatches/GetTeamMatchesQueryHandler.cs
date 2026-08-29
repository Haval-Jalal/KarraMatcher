using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Abstractions.Persistence;

namespace KarraMatcher.Application.Features.Teams.GetTeamMatches;

internal sealed class GetTeamMatchesQueryHandler(ITeamRepository teams)
    : IQueryHandler<GetTeamMatchesQuery, TeamMatchesDto?>
{
    public async Task<TeamMatchesDto?> HandleAsync(
        GetTeamMatchesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var team = await teams.FindBySlugAsync(query.Slug, cancellationToken).ConfigureAwait(false);

        if (team is null)
        {
            return null;
        }

        var matches = await teams.GetMatchesAsync(team.Id, cancellationToken).ConfigureAwait(false);

        return new TeamMatchesDto(team.ToDto(), [.. matches.Select(match => match.ToDto())]);
    }
}

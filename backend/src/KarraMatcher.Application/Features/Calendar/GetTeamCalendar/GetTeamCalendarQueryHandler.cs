using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Abstractions.Persistence;

namespace KarraMatcher.Application.Features.Calendar.GetTeamCalendar;

internal sealed class GetTeamCalendarQueryHandler(ITeamRepository teams, TimeProvider clock)
    : IQueryHandler<GetTeamCalendarQuery, string?>
{
    public async Task<string?> HandleAsync(
        GetTeamCalendarQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var team = await teams.FindBySlugAsync(query.Slug, cancellationToken).ConfigureAwait(false);

        if (team is null)
        {
            return null;
        }

        var matches = await teams.GetMatchesAsync(team.Id, cancellationToken).ConfigureAwait(false);

        return IcsCalendarBuilder.BuildFeed(team, matches, clock.GetUtcNow().UtcDateTime);
    }
}

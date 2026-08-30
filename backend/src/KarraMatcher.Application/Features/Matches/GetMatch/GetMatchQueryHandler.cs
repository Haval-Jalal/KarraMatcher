using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Teams;

namespace KarraMatcher.Application.Features.Matches.GetMatch;

internal sealed class GetMatchQueryHandler(IMatchRepository matches)
    : IQueryHandler<GetMatchQuery, MatchDetailDto?>
{
    public async Task<MatchDetailDto?> HandleAsync(
        GetMatchQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var match = await matches.FindByIdAsync(query.Id, cancellationToken).ConfigureAwait(false);

        if (match?.Team is null)
        {
            // Utan lag går matchen inte att visa: sidan behöver lagfärgen och vägen
            // tillbaka till schemat. Det ska inte kunna hända -- främmande nyckeln är
            // obligatorisk -- men ett null här vore ett 500 hos anroparen.
            return null;
        }

        return new MatchDetailDto(match.ToDto(), match.Team.ToDto());
    }
}

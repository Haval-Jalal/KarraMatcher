using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Abstractions.Persistence;

namespace KarraMatcher.Application.Features.Children;

/// <summary>
/// Ett barn i kallelse-väljaren (`#redesign`): bara id, visningsnamn ("Liam J") och färg-lag.
/// <b>Inga vårdnadshavare</b> — en färg-lag-tränare ska kunna välja barn ur hela truppen (fyll-på),
/// men inte få de vuxnas mejl på köpet (§KM.1). Det skiljer den här från admins fulla roster.
/// </summary>
public sealed record KallelseRosterChildDto(Guid Id, string DisplayName, Guid? TeamId);

/// <summary>Hela truppens barn grupperbara per färg-lag, för kallelse-väljaren.</summary>
public sealed record KallelseRosterDto(
    IReadOnlyList<RosterTeamDto> Teams,
    IReadOnlyList<KallelseRosterChildDto> Children);

/// <summary>
/// Truppens barn (utan vårdnadshavare) för kallelse-väljaren. En färg-lag-tränare når hela
/// truppen här så att hen kan fylla på individer ur andra lag — men bara läsning, och aldrig
/// mejladresser. Objektnivå-grinden ligger på endpointen (<c>CoachOfTeam</c>).
/// </summary>
public sealed record GetKallelseRosterQuery(Guid TruppId) : IQuery<KallelseRosterDto>;

internal sealed class GetKallelseRosterQueryHandler(IChildRepository children)
    : IQueryHandler<GetKallelseRosterQuery, KallelseRosterDto>
{
    public async Task<KallelseRosterDto> HandleAsync(
        GetKallelseRosterQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var teams = await children.GetTeamsForTruppAsync(query.TruppId, cancellationToken)
            .ConfigureAwait(false);
        var childList = await children.GetChildrenForTruppAsync(query.TruppId, cancellationToken)
            .ConfigureAwait(false);

        return new KallelseRosterDto(
            [.. teams.Select(team => team.ToRosterTeam())],
            [.. childList.Select(child => new KallelseRosterChildDto(
                child.Id, ChildMapping.DisplayName(child.FirstName, child.LastInitial), child.TeamId))]);
    }
}

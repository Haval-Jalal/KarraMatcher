using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Abstractions.Persistence;

namespace KarraMatcher.Application.Features.Children;

/// <summary>Truppens överblick: dess lag och barn (grupperas per lag i vyn) (`#196`).</summary>
public sealed record GetTruppRosterQuery(Guid AgeGroupId) : IQuery<TruppRosterDto>;

internal sealed class GetTruppRosterQueryHandler(IChildRepository children)
    : IQueryHandler<GetTruppRosterQuery, TruppRosterDto>
{
    public async Task<TruppRosterDto> HandleAsync(
        GetTruppRosterQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var teams = await children.GetTeamsForTruppAsync(query.AgeGroupId, cancellationToken)
            .ConfigureAwait(false);
        var childList = await children.GetChildrenForTruppAsync(query.AgeGroupId, cancellationToken)
            .ConfigureAwait(false);
        var guardianships = await children
            .GetGuardianshipsForTruppAsync(query.AgeGroupId, cancellationToken)
            .ConfigureAwait(false);

        var guardiansByChild = guardianships
            .GroupBy(g => g.ChildId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<GuardianRefDto>)[.. g.Select(x => x.ToRef())]);

        var childDtos = childList
            .Select(child => child.ToDto(
                guardiansByChild.TryGetValue(child.Id, out var guardians) ? guardians : []))
            .ToArray();

        return new TruppRosterDto([.. teams.Select(team => team.ToRosterTeam())], childDtos);
    }
}

using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Abstractions.Persistence;

namespace KarraMatcher.Application.Features.Administration;

/// <summary>Truppens lag med sina tränare, för adminvyn och superadmin-konsolen (`#197`).</summary>
public sealed record GetTruppCoachesQuery(Guid TruppId) : IQuery<TruppCoachesDto>;

internal sealed class GetTruppCoachesQueryHandler(IAdministrationRepository repository)
    : IQueryHandler<GetTruppCoachesQuery, TruppCoachesDto>
{
    public async Task<TruppCoachesDto> HandleAsync(
        GetTruppCoachesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var teams = await repository.GetLagAsync(query.TruppId, cancellationToken).ConfigureAwait(false);
        var coaches = await repository.GetCoachesForTruppAsync(query.TruppId, cancellationToken)
            .ConfigureAwait(false);

        var byTeam = coaches
            .GroupBy(role => role.TeamId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var rows = teams
            .Select(team => new CoachTeamDto(
                team.Id,
                team.Name,
                team.ColorHex,
                byTeam.TryGetValue(team.Id, out var roles)
                    ? [.. roles.Select(role => role.ToCoachDto())]
                    : []))
            .ToList();

        return new TruppCoachesDto(rows);
    }
}

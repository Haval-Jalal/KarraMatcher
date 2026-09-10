using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Carpool;

/// <summary>
/// Lagets samåkning, sedd av en tränare.
///
/// <para>
/// <c>Reader</c> är den inloggade. Den avgör vad som får visas — inte om frågan får
/// ställas. Den kontrollen ligger i policyn <c>CoachOfTeam</c> på endpointen, mot slugen i
/// adressen.
/// </para>
/// </summary>
public sealed record TeamCarpoolOverviewQuery(string Slug, Guid? Reader)
    : IQuery<IReadOnlyList<TeamCarpoolMatchDto>?>;

internal sealed class TeamCarpoolOverviewQueryHandler(CarpoolOverviewService service)
    : IQueryHandler<TeamCarpoolOverviewQuery, IReadOnlyList<TeamCarpoolMatchDto>?>
{
    public Task<IReadOnlyList<TeamCarpoolMatchDto>?> HandleAsync(
        TeamCarpoolOverviewQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return service.ForTeamAsync(query.Slug, query.Reader, cancellationToken);
    }
}

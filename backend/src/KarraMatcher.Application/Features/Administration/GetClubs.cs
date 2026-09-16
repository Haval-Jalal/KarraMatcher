using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Abstractions.Persistence;

namespace KarraMatcher.Application.Features.Administration;

/// <summary>Alla klubbar, för superadmin-konsolen (`#192`).</summary>
public sealed record GetClubsQuery : IQuery<IReadOnlyList<ClubDto>>;

internal sealed class GetClubsQueryHandler(IAdministrationRepository repository)
    : IQueryHandler<GetClubsQuery, IReadOnlyList<ClubDto>>
{
    public async Task<IReadOnlyList<ClubDto>> HandleAsync(
        GetClubsQuery query, CancellationToken cancellationToken)
    {
        var all = await repository.GetClubsAsync(cancellationToken).ConfigureAwait(false);

        return [.. all.Select(club => club.ToDto())];
    }
}

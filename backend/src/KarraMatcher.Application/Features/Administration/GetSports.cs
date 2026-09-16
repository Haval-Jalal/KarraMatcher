using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Abstractions.Persistence;

namespace KarraMatcher.Application.Features.Administration;

/// <summary>Alla sporter, för superadmin-konsolen (`#192`).</summary>
public sealed record GetSportsQuery : IQuery<IReadOnlyList<SportDto>>;

internal sealed class GetSportsQueryHandler(IAdministrationRepository repository)
    : IQueryHandler<GetSportsQuery, IReadOnlyList<SportDto>>
{
    public async Task<IReadOnlyList<SportDto>> HandleAsync(
        GetSportsQuery query, CancellationToken cancellationToken)
    {
        var all = await repository.GetSportsAsync(cancellationToken).ConfigureAwait(false);

        return [.. all.Select(sport => sport.ToDto())];
    }
}

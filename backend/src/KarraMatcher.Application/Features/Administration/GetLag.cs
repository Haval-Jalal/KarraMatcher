using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Abstractions.Persistence;

namespace KarraMatcher.Application.Features.Administration;

/// <summary>Lagen i en trupp, för superadmin-konsolen (`#192`).</summary>
public sealed record GetLagQuery(Guid TruppId) : IQuery<IReadOnlyList<LagDto>>;

internal sealed class GetLagQueryHandler(IAdministrationRepository repository)
    : IQueryHandler<GetLagQuery, IReadOnlyList<LagDto>>
{
    public async Task<IReadOnlyList<LagDto>> HandleAsync(
        GetLagQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var all = await repository.GetLagAsync(query.TruppId, cancellationToken).ConfigureAwait(false);

        return [.. all.Select(lag => lag.ToLagDto())];
    }
}

using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Abstractions.Persistence;

namespace KarraMatcher.Application.Features.Administration;

/// <summary>Trupperna, valfritt filtrerade på klubb, för superadmin-konsolen (`#192`).</summary>
public sealed record GetTrupperQuery(Guid? ClubId) : IQuery<IReadOnlyList<TruppDto>>;

internal sealed class GetTrupperQueryHandler(IAdministrationRepository repository)
    : IQueryHandler<GetTrupperQuery, IReadOnlyList<TruppDto>>
{
    public async Task<IReadOnlyList<TruppDto>> HandleAsync(
        GetTrupperQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var all = await repository.GetTrupperAsync(query.ClubId, cancellationToken).ConfigureAwait(false);

        return [.. all.Select(trupp => trupp.ToDto())];
    }
}

using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Abstractions.Persistence;

namespace KarraMatcher.Application.Features.Administration;

/// <summary>Trupperna den inloggade är admin för (`#193`) — alla för en superadmin.</summary>
public sealed record GetMyTrupperQuery(Guid AccountId, bool IsSuperAdmin)
    : IQuery<IReadOnlyList<TruppDto>>;

internal sealed class GetMyTrupperQueryHandler(IAdministrationRepository repository)
    : IQueryHandler<GetMyTrupperQuery, IReadOnlyList<TruppDto>>
{
    public async Task<IReadOnlyList<TruppDto>> HandleAsync(
        GetMyTrupperQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var trupper = await repository
            .GetTrupperForAdminAsync(query.AccountId, query.IsSuperAdmin, cancellationToken)
            .ConfigureAwait(false);

        return [.. trupper.Select(trupp => trupp.ToDto())];
    }
}

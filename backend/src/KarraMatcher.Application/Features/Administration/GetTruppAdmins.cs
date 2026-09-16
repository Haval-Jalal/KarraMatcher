using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Abstractions.Persistence;

namespace KarraMatcher.Application.Features.Administration;

/// <summary>Admins för en trupp, för superadmin-konsolen (`#192`).</summary>
public sealed record GetTruppAdminsQuery(Guid TruppId) : IQuery<IReadOnlyList<TruppAdminDto>>;

internal sealed class GetTruppAdminsQueryHandler(IAdministrationRepository repository)
    : IQueryHandler<GetTruppAdminsQuery, IReadOnlyList<TruppAdminDto>>
{
    public async Task<IReadOnlyList<TruppAdminDto>> HandleAsync(
        GetTruppAdminsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var roles = await repository.GetAdminsAsync(query.TruppId, cancellationToken).ConfigureAwait(false);

        return [.. roles.Select(role => role.ToAdminDto())];
    }
}

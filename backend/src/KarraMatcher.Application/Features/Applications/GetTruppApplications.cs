using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Abstractions.Persistence;

namespace KarraMatcher.Application.Features.Applications;

/// <summary>Väntande ansökningar för en trupp, för admin-kön (`#194`).</summary>
public sealed record GetTruppApplicationsQuery(Guid AgeGroupId)
    : IQuery<IReadOnlyList<ApplicationDto>>;

internal sealed class GetTruppApplicationsQueryHandler(IApplicationRepository applications)
    : IQueryHandler<GetTruppApplicationsQuery, IReadOnlyList<ApplicationDto>>
{
    public async Task<IReadOnlyList<ApplicationDto>> HandleAsync(
        GetTruppApplicationsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var pending = await applications
            .GetPendingForTruppAsync(query.AgeGroupId, cancellationToken)
            .ConfigureAwait(false);

        return [.. pending.Select(application => application.ToDto())];
    }
}

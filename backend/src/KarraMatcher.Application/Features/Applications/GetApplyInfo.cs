using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Applications;

/// <summary>Trupp-info för ansökningssidan (`#194`). Null om truppen inte finns.</summary>
public sealed record GetApplyInfoQuery(Guid AgeGroupId) : IQuery<ApplyInfoDto?>;

internal sealed class GetApplyInfoQueryHandler(ApplicationService service)
    : IQueryHandler<GetApplyInfoQuery, ApplyInfoDto?>
{
    public Task<ApplyInfoDto?> HandleAsync(
        GetApplyInfoQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return service.ApplyInfoAsync(query.AgeGroupId, cancellationToken);
    }
}

using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Consent;

/// <summary>Vad den inloggade har samtyckt till (`#195`).</summary>
public sealed record GetMyConsentQuery(Guid AccountId) : IQuery<MyConsentDto>;

internal sealed class GetMyConsentQueryHandler(ConsentService service)
    : IQueryHandler<GetMyConsentQuery, MyConsentDto>
{
    public Task<MyConsentDto> HandleAsync(
        GetMyConsentQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return service.MyConsentAsync(query.AccountId, cancellationToken);
    }
}

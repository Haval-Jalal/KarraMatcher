using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Consent;

/// <summary>Den aktuella samtyckestexten, att visa innan man godkänner (`#195`).</summary>
public sealed record GetCurrentConsentQuery : IQuery<ConsentTextDto>;

internal sealed class GetCurrentConsentQueryHandler
    : IQueryHandler<GetCurrentConsentQuery, ConsentTextDto>
{
    public Task<ConsentTextDto> HandleAsync(
        GetCurrentConsentQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(new ConsentTextDto(ConsentDocument.CurrentVersion, ConsentDocument.Current));
}

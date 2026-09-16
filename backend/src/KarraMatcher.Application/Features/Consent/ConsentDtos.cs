namespace KarraMatcher.Application.Features.Consent;

/// <summary>Samtyckestexten att visa innan man godkänner (§KM.6, `#195`).</summary>
/// <param name="Version">Textens version.</param>
/// <param name="Text">Själva samtyckestexten.</param>
public sealed record ConsentTextDto(string Version, string Text);

/// <summary>
/// Vad den inloggade har samtyckt till (§KM.6, `#195`).
/// </summary>
/// <param name="HasConsentedToCurrent">Sant om samtycke finns till den aktuella versionen.</param>
/// <param name="Version">Versionen man senast samtyckte till, eller null om inget samtycke finns.</param>
/// <param name="GrantedUtc">När det samtycket gavs.</param>
/// <param name="Text">Texten man samtyckte till — så det går att se exakt vad.</param>
public sealed record MyConsentDto(
    bool HasConsentedToCurrent,
    string? Version,
    DateTimeOffset? GrantedUtc,
    string? Text);

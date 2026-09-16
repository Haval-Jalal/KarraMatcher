namespace KarraMatcher.Application.Features.Administration;

/// <summary>
/// En admin för en trupp, som superadmin-konsolen visar (§KM.3, `#192`).
///
/// <para>
/// Namn och adress är den vuxnes egna uppgifter (§KM.1 rör barn, inte vuxna) och visas bara
/// för superadmin som sköter tillsättningen. De loggas aldrig (§KM.10).
/// </para>
/// </summary>
/// <param name="AccountId">Kontots id.</param>
/// <param name="DisplayName">Visningsnamnet, eller null om kontot inte fyllt i något.</param>
/// <param name="Email">Inloggningsadressen — så superadmin ser vem det är.</param>
/// <param name="GrantedUtc">När rollen tilldelades.</param>
public sealed record TruppAdminDto(
    Guid AccountId,
    string? DisplayName,
    string Email,
    DateTimeOffset GrantedUtc);

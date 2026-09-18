namespace KarraMatcher.Application.Features.Administration;

/// <summary>
/// En tränare för ett lag, som adminvyn visar (§KM.3, `#197`).
///
/// <para>
/// Namn och adress är den vuxnes egna uppgifter (§KM.1 rör barn, inte vuxna) och visas bara
/// för admin som sköter tillsättningen. De loggas aldrig (§KM.10).
/// </para>
/// </summary>
/// <param name="AccountId">Kontots id.</param>
/// <param name="DisplayName">Visningsnamnet, eller null om kontot inte fyllt i något.</param>
/// <param name="Email">Inloggningsadressen — så admin ser vem det är.</param>
/// <param name="GrantedUtc">När rollen tilldelades.</param>
public sealed record TeamCoachDto(
    Guid AccountId,
    string? DisplayName,
    string Email,
    DateTimeOffset GrantedUtc);

/// <summary>Ett lag med sina tränare — en rad i adminens tränaröversikt (`#197`).</summary>
/// <param name="TeamId">Lagets id.</param>
/// <param name="TeamName">Lagets namn (t.ex. Gul).</param>
/// <param name="ColorHex">Lagfärgen, för att spegla temat i vyn.</param>
/// <param name="Coaches">Lagets tränare, äldst först.</param>
public sealed record CoachTeamDto(
    Guid TeamId,
    string TeamName,
    string ColorHex,
    IReadOnlyList<TeamCoachDto> Coaches);

/// <summary>Truppens lag med sina tränare, för adminvyn och superadmin-konsolen (`#197`).</summary>
public sealed record TruppCoachesDto(IReadOnlyList<CoachTeamDto> Teams);

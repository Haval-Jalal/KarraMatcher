namespace KarraMatcher.Application.Features.Children;

/// <summary>En vårdnadshavare knuten till ett barn (`#196`). Vuxenuppgift, bara för admin.</summary>
/// <param name="AccountId">Kontots id.</param>
/// <param name="DisplayName">Visningsnamn, eller null om kontot inte fyllt i något.</param>
/// <param name="Email">Adressen, så admin ser vem det är.</param>
public sealed record GuardianRefDto(Guid AccountId, string? DisplayName, string Email);

/// <summary>
/// Ett barn så som admin-vyn visar det (§KM.1, `#196`).
///
/// <para>
/// Minimalt: förnamn och efternamnets initial. <see cref="DisplayName"/> är "Liam J" —
/// aldrig hela efternamnet, någonstans.
/// </para>
/// </summary>
/// <param name="Id">Barnets id.</param>
/// <param name="FirstName">Förnamnet.</param>
/// <param name="LastInitial">Efternamnets initial.</param>
/// <param name="DisplayName">Visningsnamnet, t.ex. "Liam J".</param>
/// <param name="TeamId">Laget barnet sorterats i, eller null (otilldelad).</param>
/// <param name="TeamName">Lagets namn, för visning.</param>
/// <param name="Guardians">Kopplade vårdnadshavare.</param>
public sealed record ChildDto(
    Guid Id,
    string FirstName,
    string LastInitial,
    string DisplayName,
    Guid? TeamId,
    string? TeamName,
    IReadOnlyList<GuardianRefDto> Guardians);

/// <summary>Ett lag i truppen, för att visa färg-grupperna i överblicken.</summary>
/// <param name="Id">Lagets id.</param>
/// <param name="Name">Lagets namn, t.ex. "Gul".</param>
/// <param name="ColorHex">Lagfärgen.</param>
public sealed record RosterTeamDto(Guid Id, string Name, string ColorHex);

/// <summary>Truppens överblick: dess lag och dess barn (`#196`).</summary>
/// <param name="Teams">Truppens lag (färg-grupperna).</param>
/// <param name="Children">Alla barn i truppen; gruppera på <see cref="ChildDto.TeamId"/>.</param>
public sealed record TruppRosterDto(
    IReadOnlyList<RosterTeamDto> Teams,
    IReadOnlyList<ChildDto> Children);

namespace KarraMatcher.Application.Features.Matches;

/// <summary>
/// En match så som appen visar den.
///
/// <para>
/// Avsparken lämnar servern i UTC och konverteras till Europe/Stockholm på ett enda ställe
/// i frontenden (§KM.5). Kompakt med flit: appen används på mobilnät vid fotbollsplaner
/// med dålig täckning, så bara fält som faktiskt visas följer med.
/// </para>
/// </summary>
/// <param name="Address">
/// Matchens adress — spelplatsens, om inte matchen har en avvikande adress.
/// </param>
public sealed record MatchDto(
    Guid Id,
    DateTimeOffset KickoffUtc,
    string Opponent,
    bool IsHome,
    string Status,
    string Address,
    VenueDto Venue);

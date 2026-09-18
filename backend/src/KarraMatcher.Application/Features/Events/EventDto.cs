namespace KarraMatcher.Application.Features.Events;

/// <summary>
/// En händelse så som appen visar den (`#198`).
///
/// <para>
/// Starttiden lämnar servern i UTC och konverteras till Europe/Stockholm på ett enda ställe
/// i frontenden (§KM.5). Kompakt med flit: appen används på mobilnät vid fotbollsplaner
/// med dålig täckning, så bara fält som faktiskt visas följer med.
/// </para>
///
/// <para>
/// <see cref="Opponent"/> och <see cref="IsHome"/> är satta bara för en match; en träning
/// eller övrig händelse bär i stället en <see cref="Title"/>.
/// </para>
/// </summary>
/// <param name="Type">Match, Training eller Other.</param>
/// <param name="Address">
/// Händelsens adress — spelplatsens, om inte händelsen har en avvikande adress.
/// </param>
public sealed record EventDto(
    Guid Id,
    string Type,
    DateTimeOffset KickoffUtc,
    string? Title,
    string? Opponent,
    bool? IsHome,
    string Status,
    string Address,
    VenueDto Venue);

namespace KarraMatcher.Application.Features.Events;

/// <summary>
/// En händelse som ska påminnas om i kväll (`#64`, `#198`).
///
/// <para>
/// Precis det jobbet behöver för att skicka en notis: laget att nå, och det som står i
/// notisen. För en match är det motståndare och hemma/borta; för en träning eller övrig
/// händelse rubriken. Aldrig något om ett barn (§KM.1).
/// </para>
/// </summary>
public sealed record DueEvent(
    Guid EventId,
    Guid TeamId,
    string Type,
    DateTime KickoffUtc,
    string? Title,
    string? Opponent,
    bool? IsHome,
    string VenueName);

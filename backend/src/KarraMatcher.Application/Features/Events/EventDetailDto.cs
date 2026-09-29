using KarraMatcher.Application.Features.Teams;

namespace KarraMatcher.Application.Features.Events;

/// <summary>
/// En händelse med sitt lag — vad detaljsidan behöver för att kunna visa lagfärgen och
/// länka tillbaka till schemat utan ett andra anrop.
///
/// <para>
/// Händelsens notis ingår inte. Den är tränarens fritext, som §KM.1 räknar som potentiell
/// PII, och exponeras inte i läs-svaret.
/// </para>
/// </summary>
/// <param name="Team">
/// Laget händelsen är riktad mot, eller <c>null</c> för en trupp-vid händelse (`#332`) —
/// då hör den till hela truppen och har ingen enskild lagfärg.
/// </param>
/// <param name="TruppId">
/// Åldersgruppens (truppens) id — som en admin behöver för att skicka en kallelse och hämta
/// truppens barn (§KM.7, `#199`). Bara ett id, ingen PII.
/// </param>
/// <param name="TruppName">
/// Truppens namn (t.ex. "P2016"). Låter detaljsidan visa en rubrik även för en trupp-vid
/// händelse som saknar lag (`#332`/#408) — annars fanns inget att skriva när <see cref="Team"/>
/// är null.
/// </param>
public sealed record EventDetailDto(EventDto Event, TeamDto? Team, Guid TruppId, string TruppName);

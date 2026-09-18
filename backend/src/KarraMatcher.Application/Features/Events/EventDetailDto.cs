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
public sealed record EventDetailDto(EventDto Event, TeamDto Team);

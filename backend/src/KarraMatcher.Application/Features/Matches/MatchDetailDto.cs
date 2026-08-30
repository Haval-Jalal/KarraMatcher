using KarraMatcher.Application.Features.Teams;

namespace KarraMatcher.Application.Features.Matches;

/// <summary>
/// En match med sitt lag — vad matchdetaljsidan behöver för att kunna visa lagfärgen och
/// länka tillbaka till schemat utan ett andra anrop.
///
/// <para>
/// Matchens notis ingår inte. Den är tränarens fritext, som §KM.1 räknar som potentiell
/// PII, och den här endpointen är publik och cachas på Vercels edge. Beslutet är infört i
/// <c>docs/PROJEKT-HANDOFF.md</c>; frågan tas upp igen i M3, när tränargränssnittet som
/// skapar notiser byggs.
/// </para>
/// </summary>
public sealed record MatchDetailDto(MatchDto Match, TeamDto Team);

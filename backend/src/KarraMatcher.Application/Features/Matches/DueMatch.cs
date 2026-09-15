namespace KarraMatcher.Application.Features.Matches;

/// <summary>
/// En match som ska påminnas om i kväll (`#64`).
///
/// <para>
/// Precis det jobbet behöver för att skicka en notis: laget att nå, och det som står i
/// notisen — motståndare, avspark, plats, hemma/borta. Inget mer, och aldrig något om ett
/// barn (§KM.1).
/// </para>
/// </summary>
public sealed record DueMatch(
    Guid MatchId,
    Guid TeamId,
    DateTime KickoffUtc,
    string Opponent,
    bool IsHome,
    string VenueName);

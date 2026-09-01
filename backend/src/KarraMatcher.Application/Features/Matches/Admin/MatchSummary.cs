using System.Globalization;

using KarraMatcher.Domain.Matches;

namespace KarraMatcher.Application.Features.Matches.Admin;

/// <summary>
/// Matchen som en rad i audit-loggen.
///
/// <para>
/// <b>Notisen är med flit inte med.</b> Den är tränarens egna ord och räknas som potentiell
/// PII (§KM.1) — att den ändrats får synas, men inte vad den ändrats till. Det som står här
/// är tid, motståndare, plats och status, alltså sådant som ändå ligger i den publika
/// kalendern.
/// </para>
/// </summary>
internal static class MatchSummary
{
    public static string Describe(Match match) => string.Create(
        CultureInfo.InvariantCulture,
        $"{match.KickoffUtc:yyyy-MM-ddTHH:mmZ} {match.OpponentName} {(match.IsHome ? "hemma" : "borta")} {match.Status}");

    /// <summary>Före och efter, på en rad. Tomt när ingenting av betydelse ändrats.</summary>
    public static string Change(string before, string after) =>
        before == after ? after : $"{before} -> {after}";
}

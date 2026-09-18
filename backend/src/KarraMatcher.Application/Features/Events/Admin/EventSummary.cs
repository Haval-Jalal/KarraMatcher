using System.Globalization;

using KarraMatcher.Domain.Events;

namespace KarraMatcher.Application.Features.Events.Admin;

/// <summary>
/// Händelsen som en rad i audit-loggen (`#198`).
///
/// <para>
/// <b>Notisen är med flit inte med.</b> Den är tränarens egna ord och räknas som potentiell
/// PII (§KM.1) — att den ändrats får synas, men inte vad den ändrats till. Det som står här
/// är typ, tid, motståndare/rubrik, plats och status.
/// </para>
/// </summary>
internal static class EventSummary
{
    public static string Describe(Event item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var label = item.Type == EventType.Match
            ? $"{item.OpponentName} {(item.IsHome == true ? "hemma" : "borta")}"
            : item.Title;

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{item.Type} {item.KickoffUtc:yyyy-MM-ddTHH:mmZ} {label} {item.Status}");
    }

    /// <summary>Före och efter, på en rad. Tomt när ingenting av betydelse ändrats.</summary>
    public static string Change(string before, string after) =>
        before == after ? after : $"{before} -> {after}";
}

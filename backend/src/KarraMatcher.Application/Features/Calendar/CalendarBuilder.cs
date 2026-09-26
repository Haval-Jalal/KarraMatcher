using System.Globalization;
using System.Text;

namespace KarraMatcher.Application.Features.Calendar;

/// <summary>En händelse på väg in i kalender-feeden. Ren data, ingen barn-PII (§KM.1).</summary>
public sealed record CalendarEventEntry(
    string Uid,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string Summary,
    string Location,
    bool Cancelled,
    int Sequence,
    DateTimeOffset StampUtc);

/// <summary>
/// Bygger en iCalendar-feed (RFC 5545). Ren funktion — ingen databas, ingen tid tas här — så den
/// går att pröva rad för rad.
///
/// <para>
/// Tiderna skrivs i UTC (<c>...Z</c>); kalender-appen visar dem i användarens egen zon, så §KM.5
/// hålls utan att vi behöver skicka en tidszon. Fritext escapas och långa rader viks enligt
/// standarden, annars vägrar somliga kalendrar läsa feeden.
/// </para>
/// </summary>
public static class CalendarBuilder
{
    public static string Build(string calendarName, IReadOnlyList<CalendarEventEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var builder = new StringBuilder();

        Line(builder, "BEGIN:VCALENDAR");
        Line(builder, "VERSION:2.0");
        Line(builder, "PRODID:-//Karra Matcher//Kalender//SV");
        Line(builder, "CALSCALE:GREGORIAN");
        Line(builder, "METHOD:PUBLISH");
        Line(builder, $"X-WR-CALNAME:{Escape(calendarName)}");

        foreach (var entry in entries)
        {
            Line(builder, "BEGIN:VEVENT");
            Line(builder, $"UID:{entry.Uid}");
            Line(builder, $"DTSTAMP:{Stamp(entry.StampUtc)}");
            Line(builder, $"DTSTART:{Stamp(entry.StartUtc)}");
            Line(builder, $"DTEND:{Stamp(entry.EndUtc)}");
            Line(builder, $"SUMMARY:{Escape(entry.Summary)}");

            if (!string.IsNullOrWhiteSpace(entry.Location))
            {
                Line(builder, $"LOCATION:{Escape(entry.Location)}");
            }

            Line(builder, $"SEQUENCE:{entry.Sequence.ToString(CultureInfo.InvariantCulture)}");
            Line(builder, $"STATUS:{(entry.Cancelled ? "CANCELLED" : "CONFIRMED")}");
            Line(builder, "END:VEVENT");
        }

        Line(builder, "END:VCALENDAR");

        return builder.ToString();
    }

    /// <summary>Skriver en logisk rad, vikt till högst 75 oktetter, med CRLF (RFC 5545 §3.1).</summary>
    private static void Line(StringBuilder builder, string content)
    {
        var bytes = 0;
        var first = true;

        foreach (var rune in content.EnumerateRunes())
        {
            var runeBytes = rune.Utf8SequenceLength;
            // En fortsättningsrad inleds med ett mellanslag, som räknas mot de 75 oktetterna.
            var limit = first ? 75 : 74;

            if (bytes + runeBytes > limit)
            {
                builder.Append("\r\n ");
                bytes = 1; // det inledande mellanslaget
                first = false;
            }

            builder.Append(rune.ToString());
            bytes += runeBytes;
        }

        builder.Append("\r\n");
    }

    private static string Stamp(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    /// <summary>Escapar de tecken RFC 5545 §3.3.11 kräver i ett textvärde.</summary>
    private static string Escape(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace(";", "\\;", StringComparison.Ordinal)
        .Replace(",", "\\,", StringComparison.Ordinal)
        .Replace("\r\n", "\\n", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal);
}

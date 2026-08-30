using System.Globalization;
using System.Text;

namespace KarraMatcher.Application.Features.Calendar;

/// <summary>
/// Bygger iCalendar-innehåll enligt RFC 5545.
///
/// <para>
/// Formatet är kravfyllt på sätt som är lätta att missa, och en kalenderapp som inte kan
/// tolka feeden säger sällan varför — den visar bara ingenting. De tre reglerna som fäller
/// egna implementationer:
/// </para>
///
/// <list type="bullet">
/// <item><description>
/// <b>Radslut är CRLF</b>, inte LF. En feed med LF avvisas av vissa klienter.
/// </description></item>
/// <item><description>
/// <b>Rader längre än 75 oktetter måste vikas</b> med CRLF plus ett inledande blanksteg.
/// Adresser och lagnamn passerar den gränsen lätt. Räkningen sker i <em>oktetter</em>, inte
/// tecken — ett "ä" är två oktetter i UTF-8, så en radbrytning mitt i tecknet skulle
/// förstöra det.
/// </description></item>
/// <item><description>
/// <b>Komma, semikolon, omvänt snedstreck och radbrytning måste escapas</b> i textvärden.
/// En adress som "Klarebergsvallen, Kärra" skulle annars tolkas som två värden.
/// </description></item>
/// </list>
/// </summary>
internal sealed class IcsWriter
{
    private const int MaxOctetsPerLine = 75;
    private const string LineBreak = "\r\n";

    private readonly StringBuilder _builder = new();

    /// <summary>Lägger till en rad och viker den om den är för lång.</summary>
    public void AddLine(string name, string value)
    {
        AppendFolded($"{name}:{value}");
    }

    /// <summary>Lägger till en rad vars värde är fritext och måste escapas.</summary>
    public void AddTextLine(string name, string value)
    {
        AppendFolded($"{name}:{EscapeText(value)}");
    }

    public void Begin(string component) => AddLine("BEGIN", component);

    public void End(string component) => AddLine("END", component);

    /// <summary>
    /// Formaterar ett ögonblick som UTC med Z-suffix, t.ex. <c>20260830T111500Z</c>.
    ///
    /// <para>
    /// UTC och inte lokal tid med <c>VTIMEZONE</c>. Ett Z-suffix är entydigt och kan inte
    /// bli fel: kalenderappen räknar själv om till användarens zon, vilket för föräldrarna
    /// är Europe/Stockholm. En egen <c>VTIMEZONE</c>-definition med sommartidsregler är
    /// den vanligaste orsaken till att en hemsnickrad feed visar fel timme i en app och
    /// rätt i en annan (§KM.5).
    /// </para>
    /// </summary>
    public static string FormatUtc(DateTime instant)
    {
        var utc = instant.Kind == DateTimeKind.Utc
            ? instant
            : instant.ToUniversalTime();

        return utc.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Escapar tecken som annars ändrar radens innebörd (RFC 5545 §3.3.11).
    /// Omvänt snedstreck först — annars skulle det escapa de escape-tecken vi själva lägger till.
    /// </summary>
    public static string EscapeText(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal)
            .Replace("\r\n", "\\n", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\n", StringComparison.Ordinal);

    /// <summary>
    /// Viker en rad på 75 oktetter, med ett blanksteg först på fortsättningsraden.
    ///
    /// <para>
    /// Vikningen sker på oktettgränser men aldrig mitt i ett tecken. Ett UTF-8-tecken kan
    /// vara upp till fyra oktetter, och en brytning inuti det ger obegriplig text i
    /// kalendern — ofta just i ett lagnamn med å, ä eller ö.
    /// </para>
    /// </summary>
    private void AppendFolded(string line)
    {
        var octets = Encoding.UTF8.GetByteCount(line);

        if (octets <= MaxOctetsPerLine)
        {
            _builder.Append(line).Append(LineBreak);
            return;
        }

        var remaining = line;
        var limit = MaxOctetsPerLine;

        while (remaining.Length > 0)
        {
            var take = CharactersWithinOctetLimit(remaining, limit);

            _builder.Append(remaining[..take]).Append(LineBreak);
            remaining = remaining[take..];

            if (remaining.Length > 0)
            {
                _builder.Append(' ');

                // Blanksteget upptar en oktett av fortsättningsradens utrymme.
                limit = MaxOctetsPerLine - 1;
            }
        }
    }

    /// <summary>
    /// Hur många tecken som får plats inom oktettgränsen utan att dela ett tecken.
    /// Surrogatpar hålls ihop: en emoji i ett lagnamn ska inte kunna halveras.
    /// </summary>
    private static int CharactersWithinOctetLimit(string text, int limit)
    {
        var used = 0;
        var index = 0;

        while (index < text.Length)
        {
            var length = char.IsHighSurrogate(text[index]) && index + 1 < text.Length ? 2 : 1;
            var size = Encoding.UTF8.GetByteCount(text.AsSpan(index, length));

            if (used + size > limit)
            {
                break;
            }

            used += size;
            index += length;
        }

        // Får inte returnera noll: gränsen är alltid minst 74 oktetter, så det ryms
        // åtminstone ett tecken, men en oändlig loop vore ett värre fel än en lång rad.
        return index == 0 ? Math.Min(text.Length, 1) : index;
    }

    public override string ToString() => _builder.ToString();
}

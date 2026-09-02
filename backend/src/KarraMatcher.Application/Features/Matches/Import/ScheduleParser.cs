using System.Globalization;

namespace KarraMatcher.Application.Features.Matches.Import;

/// <summary>Vad som hände med en rad.</summary>
public enum LineOutcome
{
    /// <summary>Tolkad och redo att läggas in.</summary>
    Ok,

    /// <summary>Raden hoppades över — tom, eller en rubrikrad.</summary>
    Skipped,

    /// <summary>Något saknas: färre fält än vad en match behöver.</summary>
    Incomplete,

    /// <summary>Datum eller tid gick inte att tolka.</summary>
    BadDateOrTime,

    /// <summary>Laget eller spelplatsen finns inte.</summary>
    UnknownReference,

    /// <summary>Samma match finns redan, i inklistringen eller sedan tidigare.</summary>
    Duplicate,
}

/// <summary>En rad ur inklistringen, tolkad.</summary>
public sealed record ParsedLine(
    int LineNumber,
    string RawText,
    LineOutcome Outcome,
    ParsedMatch? Match,
    string? Problem);

/// <summary>En match som gick att tolka, med referenser lösta mot befintliga poster.</summary>
public sealed record ParsedMatch(
    DateTime KickoffUtc,
    string TeamSlug,
    string Opponent,
    Guid VenueId);

/// <summary>Det parsern behöver veta om världen för att kunna lösa referenser.</summary>
public sealed record ScheduleContext(
    IReadOnlyDictionary<string, string> TeamsByName,
    IReadOnlyDictionary<string, Guid> VenuesByName,
    IReadOnlySet<string> ExistingMatchKeys,
    Func<string, string?> ToUtc);

/// <summary>
/// Tolkar inklistrad text till matcher.
///
/// <para>
/// <b>Det här är funktionen som avgör om tränarna stannar.</b> Att knappa in tjugofem
/// matcher för hand gör ingen två gånger, och en app utan matcher är en app ingen öppnar
/// igen. Därför är parsern generös med formatet och sträng med besked: varje rad får ett
/// eget svar, så tränaren ser exakt vilken rad som behöver rättas i stället för att mötas
/// av "något gick fel".
/// </para>
///
/// <para>
/// <b>Den kraschar aldrig.</b> Indata kommer från urklipp, alltså från ett system vi inte
/// styr över — det kan innehålla vad som helst. En rad som inte går att tolka blir ett
/// besked, inte ett undantag, och resten av inklistringen tolkas ändå.
/// </para>
/// </summary>
public static class ScheduleParser
{
    /// <summary>
    /// Tak på hur mycket som tolkas åt gången.
    ///
    /// <para>
    /// En säsong är trettio rader. Taket finns för att en inklistring på tio megabyte inte
    /// ska binda upp servern — och Render har en instans (checklistan 6.3).
    /// </para>
    /// </summary>
    public const int MaxLines = 500;

    private const int FieldsPerMatch = 5;

    /// <summary>Avgränsare i den ordning de prövas. Tabb först: kalkylark klistrar in så.</summary>
    private static readonly char[] Separators = ['\t', ';', ','];

    private static readonly string[] SwedishMonths =
    [
        "jan", "feb", "mar", "apr", "maj", "jun", "jul", "aug", "sep", "okt", "nov", "dec",
    ];

    public static IReadOnlyList<ParsedLine> Parse(string? text, ScheduleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var lines = text.ReplaceLineEndings("\n").Split('\n');
        var results = new List<ParsedLine>();

        // Dubbletter inom sjalva inklistringen raknas ocksa. Ett schema kopierat tva
        // ganger ar ett vanligare misstag an man tror.
        var seen = new HashSet<string>(context.ExistingMatchKeys, StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < lines.Length && index < MaxLines; index++)
        {
            results.Add(ParseLine(index + 1, lines[index], context, seen));
        }

        return results;
    }

    private static ParsedLine ParseLine(
        int number,
        string raw,
        ScheduleContext context,
        HashSet<string> seen)
    {
        var trimmed = raw.Trim();

        if (trimmed.Length == 0 || LooksLikeHeading(trimmed))
        {
            return new ParsedLine(number, raw, LineOutcome.Skipped, null, null);
        }

        var fields = Split(trimmed);

        if (fields.Length < FieldsPerMatch)
        {
            return new ParsedLine(
                number,
                raw,
                LineOutcome.Incomplete,
                null,
                $"Raden har {fields.Length} fält, men en match behöver {FieldsPerMatch}: "
                    + "datum, tid, lag, motståndare och spelplats.");
        }

        var date = ParseDate(fields[0]);
        var time = ParseTime(fields[1]);

        if (date is null || time is null)
        {
            return new ParsedLine(
                number,
                raw,
                LineOutcome.BadDateOrTime,
                null,
                "Datum eller tid gick inte att tolka. Skriv till exempel 2026-09-05 och 15:30.");
        }

        if (!context.TeamsByName.TryGetValue(Normalize(fields[2]), out var slug))
        {
            return new ParsedLine(
                number,
                raw,
                LineOutcome.UnknownReference,
                null,
                $"Laget \"{fields[2]}\" finns inte.");
        }

        if (!context.VenuesByName.TryGetValue(Normalize(fields[4]), out var venueId))
        {
            return new ParsedLine(
                number,
                raw,
                LineOutcome.UnknownReference,
                null,
                $"Spelplatsen \"{fields[4]}\" finns inte. Lägg upp den först.");
        }

        // Tiden ar svensk lokaltid -- det ar sa ett serieschema ser ut. Omrakningen sker
        // dar all annan tidsomrakning sker, inte har.
        var kickoffUtc = context.ToUtc($"{date}T{time}");

        if (kickoffUtc is null)
        {
            return new ParsedLine(
                number,
                raw,
                LineOutcome.BadDateOrTime,
                null,
                "Tiden finns inte — kontrollera datumet.");
        }

        var opponent = fields[3].Trim();

        if (opponent.Length == 0)
        {
            return new ParsedLine(
                number, raw, LineOutcome.Incomplete, null, "Motståndaren saknas.");
        }

        var key = $"{slug}|{kickoffUtc}|{Normalize(opponent)}";

        if (!seen.Add(key))
        {
            return new ParsedLine(
                number,
                raw,
                LineOutcome.Duplicate,
                null,
                "Matchen finns redan.");
        }

        return new ParsedLine(
            number,
            raw,
            LineOutcome.Ok,
            new ParsedMatch(DateTime.Parse(kickoffUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal), slug, opponent, venueId),
            null);
    }

    /// <summary>
    /// Delar raden på den avgränsare som faktiskt används.
    ///
    /// <para>
    /// Tabb prövas först, eftersom ett kalkylark klistrar in så och lagnamn kan innehålla
    /// både komma och semikolon. Att gissa fel här delar "Kareby IS, Blå" i två fält.
    /// </para>
    /// </summary>
    private static string[] Split(string line)
    {
        foreach (var separator in Separators)
        {
            if (line.Contains(separator, StringComparison.Ordinal))
            {
                return [.. line.Split(separator).Select(field => field.Trim())];
            }
        }

        // Ingen avgransare alls: da ar det inte en matchrad.
        return [line];
    }

    /// <summary>Rubrikrader från ett kopierat schema — de ska hoppas över, inte klagas på.</summary>
    private static bool LooksLikeHeading(string line)
    {
        string[] headings = ["datum", "date", "tid", "lag", "motstandare", "motståndare", "plats"];

        var first = Split(line).FirstOrDefault() ?? string.Empty;

        return headings.Contains(Normalize(first), StringComparer.Ordinal);
    }

    /// <summary>
    /// Datum på de former ett svenskt serieschema faktiskt använder.
    ///
    /// <para>
    /// Svarar med <c>yyyy-MM-dd</c>. Årtal saknas ofta i ett kopierat schema — då används
    /// årtalet som gör datumet till nästa förekomst, eftersom ett schema som klistras in
    /// gäller den säsong som kommer.
    /// </para>
    /// </summary>
    internal static string? ParseDate(string value, DateOnly? today = null)
    {
        var text = value.Trim();
        var reference = today ?? DateOnly.FromDateTime(DateTime.UtcNow);

        // 2026-09-05, 2026/09/05
        if (DateOnly.TryParseExact(
            text.Replace('/', '-'),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var iso))
        {
            return iso.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        // 5 sep, 5 september, 5/9
        var match = System.Text.RegularExpressions.Regex.Match(
            text,
            @"^(\d{1,2})[\s/.-]+([A-Za-zÅÄÖåäö]+|\d{1,2})\.?$",
            System.Text.RegularExpressions.RegexOptions.None,
            TimeSpan.FromMilliseconds(100));

        if (!match.Success)
        {
            return null;
        }

        var day = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var monthText = match.Groups[2].Value;

        int month;

        if (int.TryParse(monthText, CultureInfo.InvariantCulture, out var numericMonth))
        {
            month = numericMonth;
        }
        else
        {
            var index = Array.FindIndex(
                SwedishMonths,
                name => Normalize(monthText).StartsWith(name, StringComparison.Ordinal));

            if (index < 0)
            {
                return null;
            }

            month = index + 1;
        }

        if (month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(reference.Year, month))
        {
            return null;
        }

        var candidate = new DateOnly(reference.Year, month, day);

        // Ligger datumet mer an en manad bakat ar det nasta ars sasong. En tranare som
        // klistrar in i augusti menar hosten som kommer, inte varen som var.
        if (candidate < reference.AddMonths(-1))
        {
            candidate = candidate.AddYears(1);
        }

        return candidate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    /// <summary>Tid som <c>HH:mm</c>. Godtar både kolon och punkt.</summary>
    internal static string? ParseTime(string value)
    {
        var text = value.Trim().Replace('.', ':');

        return TimeOnly.TryParseExact(text, "H:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            || TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time)
            ? time.ToString("HH:mm", CultureInfo.InvariantCulture)
            : null;
    }

    /// <summary>Gemener utan kringrymd, för jämförelser som inte ska bry sig om skiftläge.</summary>
    internal static string Normalize(string value) => value.Trim().ToLowerInvariant();
}

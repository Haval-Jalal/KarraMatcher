using System.Globalization;

using KarraMatcher.Domain.Matches;
using KarraMatcher.Domain.Teams;

namespace KarraMatcher.Application.Features.Calendar;

/// <summary>
/// Bygger kalenderinnehåll av lagets matcher.
///
/// <para>
/// §KM.4: feeden innehåller <b>enbart</b> matchdata — lag, motståndare, tid, plats, adress
/// och status. Aldrig barnnamn, aldrig närvaro, aldrig samåkning. Feeden är publik och
/// oautentiserad, och en kalenderprenumeration ligger kvar i en telefon i åratal.
/// </para>
/// </summary>
internal static class IcsCalendarBuilder
{
    /// <summary>
    /// Domänen i UID. Måste vara stabil över tid: byter den ändras alla UID, och
    /// prenumeranternas kalendrar får dubbletter i stället för uppdateringar.
    /// </summary>
    private const string UidDomain = "karramatcher";

    private const string ProductId = "-//Karra Matcher//SV";

    /// <summary>
    /// Hur länge en match visas i kalendern. Det vi känner till är avsparken; någon
    /// sluttid finns inte i modellen.
    ///
    /// <para>
    /// En händelse utan längd blir en punkt som flera kalenderappar ritar nästan osynligt.
    /// En timme är en rimlig ruta för en ungdomsmatch och gör posten läsbar. Det är en
    /// visningsdetalj — avsparken är det som betyder något, och den är exakt.
    /// </para>
    /// </summary>
    private static readonly TimeSpan EventDuration = TimeSpan.FromHours(1);

    public static string BuildFeed(Team team, IReadOnlyList<Match> matches)
    {
        var writer = BeginCalendar();

        // Namnet kalenderappen visar för prenumerationen. Utanför standarden, men det enda
        // alla större klienter faktiskt läser — utan det heter kalendern "Kalender".
        writer.AddTextLine("X-WR-CALNAME", CalendarName(team));

        // Hur ofta klienten bör hämta om. Utan detta kan en klient vänta ett dygn, och en
        // match som flyttas på fredagen når inte fram före lördagen.
        writer.AddLine("REFRESH-INTERVAL;VALUE=DURATION", "PT6H");
        writer.AddLine("X-PUBLISHED-TTL", "PT6H");

        foreach (var match in matches)
        {
            WriteEvent(writer, team, match);
        }

        writer.End("VCALENDAR");

        return writer.ToString();
    }

    public static string BuildSingle(Team team, Match match)
    {
        var writer = BeginCalendar();

        WriteEvent(writer, team, match);
        writer.End("VCALENDAR");

        return writer.ToString();
    }

    private static IcsWriter BeginCalendar()
    {
        var writer = new IcsWriter();

        writer.Begin("VCALENDAR");
        writer.AddLine("VERSION", "2.0");
        writer.AddLine("PRODID", ProductId);
        writer.AddLine("CALSCALE", "GREGORIAN");
        writer.AddLine("METHOD", "PUBLISH");

        return writer;
    }

    private static void WriteEvent(IcsWriter writer, Team team, Match match)
    {
        writer.Begin("VEVENT");

        // UID måste vara stabilt för samma match över alla hämtningar. Ändras det får
        // prenumeranten en ny post i stället för en uppdaterad — och den gamla ligger kvar
        // med fel tid.
        writer.AddLine("UID", $"{match.Id}@{UidDomain}");

        writer.AddLine("DTSTAMP", IcsWriter.FormatUtc(Stamp(match)));
        writer.AddLine("DTSTART", IcsWriter.FormatUtc(match.KickoffUtc));
        writer.AddLine("DTEND", IcsWriter.FormatUtc(match.KickoffUtc + EventDuration));

        // SEQUENCE måste öka när matchen ändras, annars ignorerar kalendern uppdateringen
        // och föräldern står kvar med den gamla tiden (§KM.4).
        writer.AddLine(
            "SEQUENCE",
            match.IcsSequence.ToString(CultureInfo.InvariantCulture));

        writer.AddTextLine("SUMMARY", Summary(team, match));
        writer.AddTextLine("LOCATION", Location(match));
        writer.AddLine("STATUS", Status(match.Status));

        writer.End("VEVENT");
    }

    /// <summary>
    /// När informationen senast ändrades — vilket är precis vad DTSTAMP betyder.
    ///
    /// <para>
    /// Tidigare sattes den från klockan vid varje anrop. Det gjorde feeden olika för varje
    /// sekund, vilket i sin tur gjorde ETag:en värdelös: villkorade anrop fick aldrig 304,
    /// edge-cachen fick en ny tagg vid varje revalidering, och kalenderappar laddade ner
    /// hela feeden var sjätte timme fast ingenting ändrats. CI fångade det; lokalt landade
    /// båda anropen i samma sekund och testet gick grönt av ren tur.
    /// </para>
    ///
    /// <para>
    /// Med matchens egen <c>UpdatedUtc</c> är feeden byte-identisk så länge datan är det.
    /// </para>
    /// </summary>
    private static DateTime Stamp(Match match) =>
        match.UpdatedUtc == default ? match.KickoffUtc : match.UpdatedUtc;

    /// <summary>Lag och motståndare, med hemma eller borta — rubriken i kalendern.</summary>
    private static string Summary(Team team, Match match)
    {
        var direction = match.IsHome ? "hemma" : "borta";

        return $"{CalendarName(team)} ({direction}) - {match.OpponentName}";
    }

    private static string CalendarName(Team team) =>
        team.AgeGroup is null ? team.Name : $"{team.AgeGroup.Name} {team.Name}";

    /// <summary>Spelplatsen med adress. Avvikande adress vinner, precis som i API:t.</summary>
    private static string Location(Match match)
    {
        var address = string.IsNullOrWhiteSpace(match.AddressOverride)
            ? match.Venue?.Address
            : match.AddressOverride;

        var name = match.Venue?.Name;

        if (string.IsNullOrWhiteSpace(name))
        {
            return address ?? string.Empty;
        }

        return string.IsNullOrWhiteSpace(address) ? name : $"{name}, {address}";
    }

    private static string Status(MatchStatus status) => status switch
    {
        // En inställd match måste märkas CANCELLED, annars ligger den kvar som om den
        // spelades (§KM.4).
        MatchStatus.Cancelled => "CANCELLED",

        // Framflyttad utan nytt datum: tiden är osäker, och TENTATIVE är precis vad
        // RFC 5545 har för det.
        MatchStatus.Postponed => "TENTATIVE",
        _ => "CONFIRMED",
    };
}

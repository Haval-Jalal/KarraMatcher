using System.ComponentModel.DataAnnotations;

namespace KarraMatcher.Api.Caching;

/// <summary>
/// Hur länge Vercels edge får återanvända ett publikt svar, per typ av innehåll.
///
/// <para>
/// Värdena är avvägningar mellan färskhet och kallstart. En match som flyttas ska slå
/// igenom snabbt, men den vanligaste sidvisningen i hela appen — en förälder som kollar
/// avsparkstiden lördag morgon — får inte betala Renders uppvakningstid på ~50 sekunder
/// (§KM.11). Publicering av en ändring sker i requesten, så en kort fördröjning på edge
/// är priset, inte ett fel.
/// </para>
/// </summary>
public sealed class EdgeCacheOptions
{
    public const string SectionName = "EdgeCache";

    /// <summary>Lagets matchlista. Ändras sällan, läses ofta.</summary>
    [Range(0, 86400)]
    public int ScheduleSeconds { get; set; } = 300;

    /// <summary>Enskild match. Samma avvägning som schemat.</summary>
    [Range(0, 86400)]
    public int MatchDetailSeconds { get; set; } = 300;

    /// <summary>
    /// ICS-feeden. Kalenderklienter hämtar sällan och tål mer fördröjning; en ändrad
    /// match slår ändå igenom via <c>SEQUENCE</c> när klienten väl hämtar (§KM.4).
    /// </summary>
    [Range(0, 86400)]
    public int CalendarSeconds { get; set; } = 900;

    /// <summary>Spelplatser och annat som i praktiken aldrig ändras.</summary>
    [Range(0, 86400)]
    public int ReferenceSeconds { get; set; } = 3600;

    /// <summary>
    /// Hur länge edge får servera ett inaktuellt svar medan den hämtar ett nytt i
    /// bakgrunden. Det är den här inställningen som gör att en förälder aldrig står och
    /// väntar på att Render vaknar: edge svarar direkt med det gamla och uppdaterar sedan.
    /// </summary>
    [Range(0, 604800)]
    public int StaleWhileRevalidateSeconds { get; set; } = 3600;

    public int SecondsFor(EdgeCacheProfile profile) => profile switch
    {
        EdgeCacheProfile.Schedule => ScheduleSeconds,
        EdgeCacheProfile.MatchDetail => MatchDetailSeconds,
        EdgeCacheProfile.Calendar => CalendarSeconds,
        EdgeCacheProfile.Reference => ReferenceSeconds,
        _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Okänd cacheprofil."),
    };
}

using KarraMatcher.Application.Features.Push;

namespace KarraMatcher.Application.Features.Carpool;

/// <summary>
/// Notistexterna för samåkning (`#63`, §KM.12, §KM.10).
///
/// <h3>Aldrig någon fritext</h3>
///
/// <para>
/// Förarens notis, den frågandes hälsning och ett nekandes meddelande är alla fritext från
/// en förälder — potentiell PII som bara får nå de inblandade (§KM.12) och aldrig en
/// låsskärm. Notiserna här säger därför bara <em>att</em> något hänt och tar den som vill
/// veta mer in i appen. "Öppna för att se" är rätt nivå.
/// </para>
///
/// <h3>Alla länkar till matchen</h3>
///
/// <para>
/// Samåkningen bor på matchsidan, så ett klick på notisen tar föräldern dit den ska —
/// erbjudandet, förfrågan eller svaret finns där.
/// </para>
/// </summary>
internal static class CarpoolNotification
{
    /// <summary>Nytt erbjudande — till lagets prenumeranter.</summary>
    public static PushMessage NewOffer(Guid matchId) => new(
        "Ny samåkning",
        "Någon erbjuder skjuts till en match. Öppna för att se.",
        Url(matchId));

    /// <summary>Ny förfrågan — till föraren.</summary>
    public static PushMessage NewRequest(Guid matchId) => new(
        "Ny åkförfrågan",
        "Någon vill åka med. Öppna för att svara.",
        Url(matchId));

    /// <summary>Svar på en förfrågan — till den som frågade.</summary>
    public static PushMessage RequestAnswered(Guid matchId) => new(
        "Svar på din åkförfrågan",
        "Föraren har svarat. Öppna för att se.",
        Url(matchId));

    /// <summary>Tillbakadraget erbjudande — till dem som accepterats.</summary>
    public static PushMessage OfferWithdrawn(Guid matchId) => new(
        "En skjuts drogs tillbaka",
        "Ett erbjudande du var med på har dragits tillbaka. Öppna för att se.",
        Url(matchId));

    /// <summary>Ny skjutsförfrågan (en förälder ber om skjuts) — till lagets prenumeranter.</summary>
    public static PushMessage NewRideRequest(Guid matchId) => new(
        "Någon behöver skjuts",
        "En förälder frågar efter skjuts till en match. Öppna för att se.",
        Url(matchId));

    /// <summary>Nytt platserbjudande på en skjutsförfrågan — till den som frågade.</summary>
    public static PushMessage NewRideOffer(Guid matchId) => new(
        "Någon kan köra dig",
        "En förare har erbjudit plats. Öppna för att svara.",
        Url(matchId));

    /// <summary>Svar på ett platserbjudande — till föraren som erbjöd plats.</summary>
    public static PushMessage RideOfferAnswered(Guid matchId) => new(
        "Svar på ditt platserbjudande",
        "Föräldern har svarat. Öppna för att se.",
        Url(matchId));

    /// <summary>Tillbakadragen skjutsförfrågan — till förare med ett aktivt platserbjudande.</summary>
    public static PushMessage RideRequestWithdrawn(Guid matchId) => new(
        "En skjutsförfrågan drogs tillbaka",
        "En förfrågan du erbjöd plats på har dragits tillbaka. Öppna för att se.",
        Url(matchId));

    // Samåkningen bär internt `MatchId`, men det pekar på en händelse (§KM.12, `#198`), så
    // djuplänken är händelsesidan `/handelse/{id}` — inte den utgångna `/match/`-adressen.
    private static string Url(Guid matchId) => $"/handelse/{matchId}";
}

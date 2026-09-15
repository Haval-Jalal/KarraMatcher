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

    private static string Url(Guid matchId) => $"/match/{matchId}";
}

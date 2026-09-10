namespace KarraMatcher.Application.Features.Carpool;

/// <summary>
/// En match i tränarens samåkningsöverblick (`#55`).
///
/// <h3>Frågan raden ska svara på</h3>
///
/// <para>
/// "Får alla skjuts till bortamatchen?" Därför är matcher <em>utan</em> erbjudanden med i
/// listan — det är de raderna som betyder något. En överblick som bara visade det som
/// redan är ordnat hade varit trevlig att titta på och oanvändbar att agera på.
/// </para>
///
/// <h3>Vad som inte finns här</h3>
///
/// <para>
/// <b>Inga namn.</b> Servern lagrar inget namn på en förälder — bara en mejladress, och den
/// visas aldrig för någon annan. Överblicken är därför räknad, inte namngiven.
/// </para>
///
/// <para>
/// <b>Ingen hälsning från den som frågar.</b> §KM.12 tillåter tränaren att se fritexten,
/// men överblicken behöver den inte för att svara på sin fråga — och det som inte behövs
/// ska inte hämtas. Förarens notis följer med, eftersom den ofta säger var bilen går ifrån
/// och när.
/// </para>
/// </summary>
public sealed record TeamCarpoolMatchDto(
    Guid MatchId,
    DateTime KickoffUtc,
    string Opponent,
    bool IsHome,
    IReadOnlyList<CarpoolOfferDto> Offers,
    int PendingRequests)
{
    /// <summary>Platser som erbjudits, oavsett om de är tagna.</summary>
    public int SeatsOffered => Offers.Sum(offer => offer.Seats);

    /// <summary>Platser som faktiskt är bokade — alltså accepterade förfrågningar.</summary>
    public int SeatsTaken => Offers.Sum(offer => offer.SeatsTaken);

    /// <summary>Platser kvar att erbjuda någon.</summary>
    public int SeatsLeft => Offers.Sum(offer => offer.SeatsLeft);

    /// <summary>Sant när ingen erbjudit skjuts än. Den rad tränaren ska reagera på.</summary>
    public bool NeedsDriver => Offers.Count == 0;
}

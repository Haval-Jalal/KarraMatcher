using KarraMatcher.Domain.Carpool;

namespace KarraMatcher.Application.Features.Carpool;

/// <summary>
/// Ett erbjudande så som en läsare ser det.
///
/// <h3>"Fullt" räknas fram, det lagras inte</h3>
///
/// <para>
/// <see cref="SeatsTaken"/> är summan av de accepterade förfrågningarnas platser (§KM.12).
/// Erbjudandet självt har inget fullt-tillstånd: hade det haft ett, vore det ett andra ställe
/// som vet samma sak, och varje ställe som ändrar en förfrågan hade behövt komma ihåg att
/// hålla det i takt. Ett fullt erbjudande ska dessutom fortsätta synas i listan och gå att
/// fråga om — så det får inte vara ett tillstånd som filtrerar bort det.
/// </para>
/// </summary>
public sealed record CarpoolOfferDto(
    Guid Id,
    Guid MatchId,
    CarpoolDirection Direction,
    string DeparturePlace,
    DateTime DepartureUtc,
    int Seats,
    int SeatsTaken,
    string? Note,
    bool IsMine,
    string? DriverName)
{
    /// <summary>Platser kvar. Aldrig negativt — se <see cref="SeatsTaken"/>.</summary>
    public int SeatsLeft => Math.Max(0, Seats - SeatsTaken);

    /// <summary>
    /// Sant när platserna tagit slut.
    ///
    /// <para>
    /// Betyder <em>inte</em> att erbjudandet är stängt. Det syns kvar och går att fråga om,
    /// så att föraren kan svara "någon annan hann före" i stället för att den som frågar möts
    /// av en död knapp (§KM.12).
    /// </para>
    /// </summary>
    public bool IsFull => SeatsLeft == 0;

    /// <summary>
    /// Bygger svaret för en läsare.
    ///
    /// <para>
    /// <paramref name="driverName"/> lämnas som null för en gäst, precis som notisen. Vem
    /// som kör är lagets sak och inte hela internets (§KM.3) — och skulle någon anropare
    /// glömma det står gästen ändå utan namn, eftersom <paramref name="reader"/> avgör.
    /// </para>
    /// </summary>
    public static CarpoolOfferDto For(
        CarpoolOffer offer,
        Guid? reader,
        int seatsTaken = 0,
        string? driverName = null)
    {
        ArgumentNullException.ThrowIfNull(offer);

        return new CarpoolOfferDto(
            offer.Id,
            offer.MatchId,
            offer.Direction,
            offer.DeparturePlace,
            offer.DepartureUtc,
            offer.Seats,
            seatsTaken,
            reader is null ? null : offer.Note,
            reader == offer.DriverAccountId,
            reader is null ? null : driverName);
    }
}

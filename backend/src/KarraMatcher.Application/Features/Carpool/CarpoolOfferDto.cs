using KarraMatcher.Domain.Carpool;

namespace KarraMatcher.Application.Features.Carpool;

/// <summary>
/// Ett erbjudande så som klienten ser det.
///
/// <h3>Notisen följer inte alltid med</h3>
///
/// <para>
/// Erbjudandena går att se utan konto (§KM.3), men fritext är potentiell PII och ska bara
/// nå de inblandade (§KM.12). Notisen fylls därför bara i för en inloggad läsare — en
/// utloggad får erbjudandet utan den, inte ett tomt svar.
/// </para>
///
/// <para>
/// Det är också vad som gör svaret ofarligt att svara på från en öppen adress: två läsare
/// av samma erbjudande kan få olika mycket, så svaret får aldrig ligga i en delad cache.
/// </para>
/// </summary>
public sealed record CarpoolOfferDto(
    Guid Id,
    Guid MatchId,
    CarpoolDirection Direction,
    string DeparturePlace,
    DateTime DepartureUtc,
    int Seats,
    string? Note,
    bool IsMine)
{
    /// <summary>
    /// Bygger svaret för en viss läsare.
    /// </summary>
    /// <param name="offer">Erbjudandet.</param>
    /// <param name="reader">Den inloggades konto-id, eller null för en gäst.</param>
    public static CarpoolOfferDto For(CarpoolOffer offer, Guid? reader)
    {
        ArgumentNullException.ThrowIfNull(offer);

        return new CarpoolOfferDto(
            offer.Id,
            offer.MatchId,
            offer.Direction,
            offer.DeparturePlace,
            offer.DepartureUtc,
            offer.Seats,
            reader is null ? null : offer.Note,
            reader == offer.DriverAccountId);
    }
}

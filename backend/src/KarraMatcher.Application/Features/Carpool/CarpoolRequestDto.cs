using KarraMatcher.Domain.Carpool;

namespace KarraMatcher.Application.Features.Carpool;

/// <summary>
/// En förfrågan så som de inblandade ser den.
///
/// <para>
/// <b>Ingen publik variant finns.</b> Till skillnad från erbjudandet, som vem som helst får
/// se (§KM.3), är förfrågan något mellan två föräldrar — och hälsningen är fritext som bara
/// får nå de inblandade och lagets tränare (§KM.12). Endpointen som svarar med den här
/// kräver därför inloggning, och filtrerar dessutom på vem som frågar.
/// </para>
/// </summary>
public sealed record CarpoolRequestDto(
    Guid Id,
    Guid OfferId,
    int Seats,
    string? Message,
    string? ResponseMessage,
    CarpoolRequestStatus Status,
    DateTime CreatedUtc,
    bool IsMine,
    string? RequesterName)
{
    /// <summary>
    /// Bygger svaret för en läsare.
    ///
    /// <para>
    /// Listan når bara föraren och den som frågat (§KM.12), så namnet är redan begränsat
    /// till de inblandade när det kommer hit.
    /// </para>
    /// </summary>
    public static CarpoolRequestDto For(
        CarpoolRequest request,
        Guid reader,
        string? requesterName = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new CarpoolRequestDto(
            request.Id,
            request.OfferId,
            request.Seats,
            request.Message,
            request.ResponseMessage,
            request.Status,
            request.CreatedUtc,
            request.RequesterAccountId == reader,
            requesterName);
    }
}

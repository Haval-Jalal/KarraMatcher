using KarraMatcher.Domain.Carpool;

namespace KarraMatcher.Application.Features.Carpool;

/// <summary>
/// En skjutsförfrågan så lagets medlemmar ser den.
///
/// <para>
/// De lagsynliga fälten (riktning, antal, notis, vem som frågar) motsvarar ett erbjudandes — alla
/// medlemmar får se att någon behöver skjuts. De tätare orden (förares platserbjudanden och
/// förälderns svar) bor på <see cref="CarpoolRideOfferDto"/> och når bara de inblandade (§KM.12).
/// </para>
/// </summary>
public sealed record CarpoolRideRequestDto(
    Guid Id,
    CarpoolDirection Direction,
    int Seats,
    string? Note,
    CarpoolRideRequestStatus Status,
    DateTime CreatedUtc,
    bool IsMine,
    string? RequesterName)
{
    public static CarpoolRideRequestDto For(
        CarpoolRideRequest request,
        Guid reader,
        string? requesterName = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new CarpoolRideRequestDto(
            request.Id,
            request.Direction,
            request.Seats,
            request.Note,
            request.Status,
            request.CreatedUtc,
            request.RequesterAccountId == reader,
            requesterName);
    }
}

/// <summary>
/// Ett förares platserbjudande på en skjutsförfrågan, sett av en av de två inblandade.
///
/// <para>
/// Ingen publik variant: hälsningen och förälderns svar är fritext mellan två föräldrar (§KM.12).
/// Endpointen filtrerar på läsaren — den som frågade ser alla erbjudanden på sin förfrågan, en
/// förare ser bara sitt eget.
/// </para>
/// </summary>
public sealed record CarpoolRideOfferDto(
    Guid Id,
    Guid RideRequestId,
    int Seats,
    string? Message,
    string? ResponseMessage,
    CarpoolRequestStatus Status,
    DateTime CreatedUtc,
    bool IsMine,
    string? DriverName)
{
    public static CarpoolRideOfferDto For(
        CarpoolRideOffer offer,
        Guid reader,
        string? driverName = null)
    {
        ArgumentNullException.ThrowIfNull(offer);

        return new CarpoolRideOfferDto(
            offer.Id,
            offer.RideRequestId,
            offer.Seats,
            offer.Message,
            offer.ResponseMessage,
            offer.Status,
            offer.CreatedUtc,
            offer.DriverAccountId == reader,
            driverName);
    }
}

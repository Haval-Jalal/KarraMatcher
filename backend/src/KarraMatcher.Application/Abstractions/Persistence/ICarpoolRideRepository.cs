using KarraMatcher.Domain.Carpool;

namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>
/// Läser och skriver skjutsförfrågningar (en förälder ber om skjuts) och förares platserbjudanden
/// på dem — spegelbilden av erbjudande/åkförfrågan (§KM.12, `#63`).
/// </summary>
public interface ICarpoolRideRepository
{
    // ---- Skjutsförfrågan (passagerarens sida) ----------------------------------------

    public Task AddRequestAsync(CarpoolRideRequest request, CancellationToken cancellationToken);

    /// <summary>Förfrågan, spårad för ändring. Även lösta/tillbakadragna — ägarkontrollen görs på den.</summary>
    public Task<CarpoolRideRequest?> FindRequestForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken);

    /// <summary>
    /// Matchens <b>öppna</b> skjutsförfrågningar, äldst först. Lösta och tillbakadragna kommer inte
    /// med — de syns inte längre som något förare kan erbjuda plats på.
    /// </summary>
    public Task<IReadOnlyList<CarpoolRideRequest>> ListOpenForMatchAsync(
        Guid matchId,
        CancellationToken cancellationToken);

    // ---- Platserbjudande (förarens sida) ---------------------------------------------

    public Task AddOfferAsync(CarpoolRideOffer offer, CancellationToken cancellationToken);

    /// <summary>Platserbjudandet, spårat för ändring.</summary>
    public Task<CarpoolRideOffer?> FindOfferForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken);

    /// <summary>
    /// Sant när föraren redan har ett platserbjudande som väntar eller är accepterat på förfrågan.
    /// Ger det begripliga felet; garantin ligger i ett filtrerat unikt index.
    /// </summary>
    public Task<bool> HasActiveOfferAsync(
        Guid rideRequestId,
        Guid driverAccountId,
        CancellationToken cancellationToken);

    /// <summary>En förfrågans platserbjudanden, äldst först — den som erbjöd först syns först.</summary>
    public Task<IReadOnlyList<CarpoolRideOffer>> ListOffersForRequestAsync(
        Guid rideRequestId,
        CancellationToken cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken);
}

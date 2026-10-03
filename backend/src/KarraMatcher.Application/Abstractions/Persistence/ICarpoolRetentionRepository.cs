namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>
/// Gallringen av samåkning (§KM.12, säkerhetschecklistan 9.8).
///
/// <para>
/// Egen abstraktion och inte en metod på erbjudande-repositoryt: gallringen är inte en
/// del av flödet mellan föräldrar, den är en tidsstyrd radering som sker utan att någon
/// bett om det. Att den syns som något eget gör den svårare att råka anropa fel, och
/// lättare att hitta för den som frågar var datan tar vägen.
/// </para>
/// </summary>
public interface ICarpoolRetentionRepository
{
    /// <summary>
    /// Raderar all samåkning för matcher som spelades före <paramref name="cutoffUtc"/>.
    ///
    /// <para>
    /// Riktig radering, inte en flagga. En "raderad"-markering hade lämnat kvar
    /// föräldrarnas fritext i databasen, och det är precis den gallringen finns för att bli
    /// av med.
    /// </para>
    /// </summary>
    /// <returns>Antal raderade förfrågningar och erbjudanden.</returns>
    public Task<CarpoolPurgeResult> PurgeAsync(DateTime cutoffUtc, CancellationToken cancellationToken);
}

/// <summary>
/// Vad en gallring tog med sig. Bara siffror — aldrig innehåll (§KM.10).
///
/// <para>
/// <see cref="RideRequests"/>/<see cref="RideOffers"/> räknar skjutsförfrågningarna och deras
/// platserbjudanden (`#63`-spegeln). Positionellt sist med standardvärden så äldre anrop som bara
/// bryr sig om erbjudande/åkförfrågan är oförändrade.
/// </para>
/// </summary>
public sealed record CarpoolPurgeResult(
    int Requests,
    int Offers,
    int RideRequests = 0,
    int RideOffers = 0)
{
    public static CarpoolPurgeResult Nothing { get; } = new(0, 0);

    public bool RemovedAnything => Requests > 0 || Offers > 0 || RideRequests > 0 || RideOffers > 0;
}

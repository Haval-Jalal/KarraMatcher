namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>
/// Läser och sätter kallelsens flagga per lag (§KM.7).
///
/// <para>
/// Egen abstraktion och inte en metod på <see cref="ITeamRepository"/>: flaggan är inte en
/// egenskap hos laget som appen visar, den är en grind. Att den syns som något eget gör
/// det svårare att råka läsa den som "en till kolumn" — och lättare att hitta för den som
/// undrar var kallelsen slås på.
/// </para>
/// </summary>
public interface IAttendanceRepository
{
    /// <summary>
    /// Är kallelsen påslagen för laget? Null när laget inte finns.
    ///
    /// <para>
    /// Skillnaden mellan <c>false</c> och <c>null</c> spelar ingen roll utåt — båda blir
    /// <c>404</c> (§KM.7). Den finns för att den som läser koden ska se att frågan är
    /// ställd, inte för att svaret ska skilja sig.
    /// </para>
    /// </summary>
    public Task<bool?> IsEnabledForTeamAsync(string slug, CancellationToken cancellationToken);

    /// <summary>Samma fråga, ställd från en match. Null när matchen inte finns.</summary>
    public Task<bool?> IsEnabledForMatchAsync(Guid matchId, CancellationToken cancellationToken);

    /// <summary>
    /// Slår på eller av kallelsen för ett lag.
    ///
    /// <para>
    /// Sparar <b>inte</b>. Ändringen och audit-raden hör ihop och ska stå eller falla
    /// tillsammans — anroparen avslutar med <see cref="SaveChangesAsync"/>.
    /// </para>
    /// </summary>
    /// <returns>Lagets id, eller null om laget inte finns.</returns>
    public Task<Guid?> SetEnabledAsync(string slug, bool enabled, CancellationToken cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken);
}

using KarraMatcher.Domain.Carpool;
using KarraMatcher.Domain.Events;

namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>
/// Ett samåkningserbjudandes händelse: dess lag och typ. Typen låter tjänsten avvisa samåkning
/// för allt som inte är en match (§KM.12) — samma gräns som FE visar, men som den riktiga
/// grinden (`#291`).
/// </summary>
public sealed record CarpoolEventTarget(Guid TeamId, EventType Type);

/// <summary>Läser och skriver samåkningserbjudanden.</summary>
public interface ICarpoolOfferRepository
{
    public Task<bool> MatchExistsAsync(Guid matchId, CancellationToken cancellationToken);

    /// <summary>
    /// Matchens lag, eller null när matchen inte finns. Behövs för att rikta notisen om ett
    /// nytt erbjudande till rätt lags prenumeranter (`#63`).
    /// </summary>
    public Task<Guid?> FindMatchTeamIdAsync(Guid matchId, CancellationToken cancellationToken);

    /// <summary>
    /// Händelsens lag och typ, eller null när den inte finns. Låter <c>CreateAsync</c> avvisa
    /// samåkning för en träning eller övrig händelse innan ett erbjudande skapas (`#291`).
    /// </summary>
    public Task<CarpoolEventTarget?> FindEventTargetAsync(
        Guid eventId,
        CancellationToken cancellationToken);

    /// <summary>Erbjudandet, spårat för ändring. Även tillbakadragna — ägarkontrollen görs på det.</summary>
    public Task<CarpoolOffer?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Matchens öppna erbjudanden, i avgångsordning.
    ///
    /// <para>
    /// Tillbakadragna kommer inte med. De ligger kvar i databasen tills gallringen tar dem
    /// (§KM.12), men de ska inte synas som bokningsbara.
    /// </para>
    /// </summary>
    public Task<IReadOnlyList<CarpoolOffer>> ListOpenForMatchAsync(
        Guid matchId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Öppna erbjudanden för flera matcher på en gång, i avgångsordning.
    ///
    /// <para>
    /// Finns för tränarens överblick, som annars hade ställt en fråga per match i
    /// säsongsresten. Tom lista in ger tom lista ut.
    /// </para>
    /// </summary>
    public Task<IReadOnlyList<CarpoolOffer>> ListOpenForMatchesAsync(
        IReadOnlyCollection<Guid> matchIds,
        CancellationToken cancellationToken);

    public Task AddAsync(CarpoolOffer offer, CancellationToken cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken);
}

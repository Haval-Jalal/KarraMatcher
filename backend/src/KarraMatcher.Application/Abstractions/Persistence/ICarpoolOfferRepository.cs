using KarraMatcher.Domain.Carpool;

namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>Läser och skriver samåkningserbjudanden.</summary>
public interface ICarpoolOfferRepository
{
    public Task<bool> MatchExistsAsync(Guid matchId, CancellationToken cancellationToken);

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

using KarraMatcher.Domain.Carpool;

namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>Läser och skriver åkförfrågningar.</summary>
public interface ICarpoolRequestRepository
{
    public Task<CarpoolRequest?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Sant när kontot redan har en förfrågan som väntar eller är accepterad på erbjudandet.
    /// </summary>
    /// <remarks>
    /// Ger det begripliga felet. Garantin ligger i ett filtrerat unikt index — två anrop
    /// som kommer samtidigt hinner båda läsa "nej" innan någon av dem skrivit.
    /// </remarks>
    public Task<bool> HasActiveAsync(
        Guid offerId,
        Guid requesterAccountId,
        CancellationToken cancellationToken);

    /// <summary>Erbjudandets förfrågningar, äldst först — den som frågade först syns först.</summary>
    public Task<IReadOnlyList<CarpoolRequest>> ListForOfferAsync(
        Guid offerId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Summan av platserna på erbjudandets <b>accepterade</b> förfrågningar.
    ///
    /// <para>
    /// Bara accepterade räknas (§KM.12) — att fråga tar ingen plats i anspråk. Summan räknas
    /// i databasen i stället för att lagras på erbjudandet: ett sparat "upptaget"-tal är ett
    /// andra ställe som vet samma sak, och det är det som glider isär.
    /// </para>
    /// </summary>
    public Task<int> AcceptedSeatsAsync(Guid offerId, CancellationToken cancellationToken);

    /// <summary>
    /// Samma räkning för flera erbjudanden på en gång.
    ///
    /// <para>
    /// Finns för att listningen av en matchs erbjudanden inte ska ställa en fråga per
    /// erbjudande. Erbjudanden utan accepterade förfrågningar saknas i svaret.
    /// </para>
    /// </summary>
    public Task<IReadOnlyDictionary<Guid, int>> AcceptedSeatsForOffersAsync(
        IReadOnlyCollection<Guid> offerIds,
        CancellationToken cancellationToken);

    public Task AddAsync(CarpoolRequest request, CancellationToken cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken);
}

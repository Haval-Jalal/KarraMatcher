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

    public Task AddAsync(CarpoolRequest request, CancellationToken cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken);
}

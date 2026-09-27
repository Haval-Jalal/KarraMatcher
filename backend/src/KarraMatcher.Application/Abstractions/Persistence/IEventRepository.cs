using KarraMatcher.Domain.Events;

namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>Läsåtkomst till enskilda händelser.</summary>
public interface IEventRepository
{
    /// <summary>
    /// En händelse med spelplats och lag inlästa, eller null om den inte finns.
    /// </summary>
    public Task<Event?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Alla händelser i en trupp (alla typer, alla lag <em>och</em> trupp-vida) i avsparksordning,
    /// med lag och plats inlästa (`#334`). Truppens medlemmar ser hela listan — endpointen vaktar
    /// medlemskapet (§KM.3).
    /// </summary>
    public Task<IReadOnlyList<Event>> ListByTruppAsync(Guid ageGroupId, CancellationToken cancellationToken);
}

using KarraMatcher.Domain.Events;

namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>Läsåtkomst till enskilda händelser.</summary>
public interface IEventRepository
{
    /// <summary>
    /// En händelse med spelplats och lag inlästa, eller null om den inte finns.
    /// </summary>
    public Task<Event?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
}

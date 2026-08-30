using KarraMatcher.Domain.Matches;

namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>Läsåtkomst till enskilda matcher.</summary>
public interface IMatchRepository
{
    /// <summary>
    /// En match med spelplats och lag inlästa, eller null om den inte finns.
    /// </summary>
    public Task<Match?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
}

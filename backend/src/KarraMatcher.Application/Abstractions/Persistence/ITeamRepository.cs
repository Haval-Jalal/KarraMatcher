using KarraMatcher.Domain.Matches;
using KarraMatcher.Domain.Teams;

namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>
/// Läsåtkomst till lag och deras matcher. Interfacet bor i Application och
/// implementeras i Infrastructure — beroendet pekar inåt.
/// </summary>
public interface ITeamRepository
{
    /// <summary>Alla lag, med åldersgrupp inläst. Sorterade på namn.</summary>
    public Task<IReadOnlyList<Team>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>Ett lag på dess slug, eller null om det inte finns.</summary>
    public Task<Team?> FindBySlugAsync(string slug, CancellationToken cancellationToken);

    /// <summary>Lagets matcher med spelplats inläst, sorterade på avspark.</summary>
    public Task<IReadOnlyList<Match>> GetMatchesAsync(Guid teamId, CancellationToken cancellationToken);
}

using KarraMatcher.Domain.Matches;
using KarraMatcher.Domain.Teams;

namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>
/// Skrivåtkomst till matcher — tränarens verktyg.
///
/// <para>
/// Skild från <see cref="IMatchRepository"/>, som bara läser. Uppdelningen speglar CQRS
/// och gör det svårt att av misstag skriva från en läsväg: de publika endpointsen ser
/// aldrig den här.
/// </para>
/// </summary>
public interface IMatchAdminRepository
{
    /// <summary>Matchen med lag och spelplats, spårad för ändring.</summary>
    public Task<Match?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken);

    public Task<Team?> FindTeamBySlugAsync(string slug, CancellationToken cancellationToken);

    public Task<bool> VenueExistsAsync(Guid venueId, CancellationToken cancellationToken);

    public Task AddAsync(Match match, CancellationToken cancellationToken);

    public void Remove(Match match);

    public Task SaveChangesAsync(CancellationToken cancellationToken);
}

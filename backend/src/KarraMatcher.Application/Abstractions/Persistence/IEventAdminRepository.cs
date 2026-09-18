using KarraMatcher.Domain.Events;
using KarraMatcher.Domain.Teams;

namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>
/// Skrivåtkomst till händelser — tränarens verktyg.
///
/// <para>
/// Skild från <see cref="IEventRepository"/>, som bara läser. Uppdelningen speglar CQRS
/// och gör det svårt att av misstag skriva från en läsväg.
/// </para>
/// </summary>
public interface IEventAdminRepository
{
    /// <summary>Händelsen med lag och spelplats, spårad för ändring.</summary>
    public Task<Event?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken);

    public Task<Team?> FindTeamBySlugAsync(string slug, CancellationToken cancellationToken);

    public Task<bool> VenueExistsAsync(Guid venueId, CancellationToken cancellationToken);

    public Task AddAsync(Event item, CancellationToken cancellationToken);

    public void Remove(Event item);

    public Task SaveChangesAsync(CancellationToken cancellationToken);
}

using KarraMatcher.Domain.Calendar;
using KarraMatcher.Domain.Events;

namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>
/// Kalender-nycklarna och händelserna bakom kalender-feeden (kalender bakom medlemskap, §KM.4).
/// </summary>
public interface ICalendarRepository
{
    /// <summary>Kontots nyckel, eller null om ingen skapats än.</summary>
    public Task<CalendarToken?> FindByAccountAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>
    /// Kontot en nyckel pekar på, eller null om nyckeln är okänd/återkallad. Noterar samtidigt att
    /// feeden hämtades (<see cref="CalendarToken.LastUsedUtc"/>).
    /// </summary>
    public Task<Guid?> ResolveAccountAsync(string token, CancellationToken cancellationToken);

    public Task AddAsync(CalendarToken token, CancellationToken cancellationToken);

    public void Remove(CalendarToken token);

    public Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Händelserna i en uppsättning lag från och med <paramref name="fromUtc"/>, med spelplats och
    /// klubb laddade så platsen kan lösas som i schemat. Inställda tas med (märks i feeden).
    /// </summary>
    public Task<IReadOnlyList<Event>> EventsForTeamsAsync(
        IReadOnlyCollection<Guid> teamIds,
        DateTime fromUtc,
        CancellationToken cancellationToken);
}

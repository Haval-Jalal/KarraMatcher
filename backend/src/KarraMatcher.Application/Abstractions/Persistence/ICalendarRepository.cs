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
    /// Händelserna medlemmen ser från och med <paramref name="fromUtc"/>: lag-riktade i något av
    /// <paramref name="teamIds"/>, plus trupp-övergripande (utan lag) i någon av
    /// <paramref name="truppIds"/> (`#332`, #475). Spelplats, lag och trupp laddas så platsen och
    /// etiketten kan lösas som i schemat. Inställda tas med (märks i feeden).
    /// </summary>
    public Task<IReadOnlyList<Event>> EventsForTeamsAsync(
        IReadOnlyCollection<Guid> teamIds,
        IReadOnlyCollection<Guid> truppIds,
        DateTime fromUtc,
        CancellationToken cancellationToken);
}

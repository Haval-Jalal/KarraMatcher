using KarraMatcher.Domain.Push;

namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>Läser och skriver en förälders notisinställningar per lag (`#65`).</summary>
public interface INotificationPreferenceRepository
{
    /// <summary>Kontots inställning för laget, spårad för ändring. Null när ingen finns än.</summary>
    public Task<NotificationPreference?> FindAsync(
        Guid accountId,
        Guid teamId,
        CancellationToken cancellationToken);

    public Task AddAsync(NotificationPreference preference, CancellationToken cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken);
}

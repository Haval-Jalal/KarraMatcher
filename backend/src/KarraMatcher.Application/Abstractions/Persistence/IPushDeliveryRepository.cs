using KarraMatcher.Application.Abstractions.Push;

namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>
/// Utskickets egen läsväg till prenumerationerna (`#61`).
///
/// <para>
/// <see cref="IPushSubscriptionRepository"/> lämnar med flit aldrig ut en push-adress —
/// den är en personuppgift och behövs bara här. Att läsvägen är ett eget interface gör det
/// synligt vem som får se adresserna: utskicket, och ingen annan.
/// </para>
/// </summary>
public interface IPushDeliveryRepository
{
    /// <summary>Lagets prenumeranter, med det som krävs för att kryptera åt dem.</summary>
    public Task<IReadOnlyList<PushTarget>> ListForTeamAsync(
        Guid teamId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Tar bort prenumerationer som push-tjänsten sagt är borta.
    /// </summary>
    /// <remarks>
    /// Flera på en gång: ett utskick till ett helt lag hittar ofta några döda samtidigt, och
    /// en radering per rad hade blivit en fråga per död enhet.
    /// </remarks>
    public Task RemoveAsync(IReadOnlyCollection<Guid> subscriptionIds, CancellationToken cancellationToken);

    /// <summary>Noterar att utskicket gick fram, så gallringen vet vad som lever.</summary>
    public Task MarkDeliveredAsync(
        IReadOnlyCollection<Guid> subscriptionIds,
        CancellationToken cancellationToken);
}

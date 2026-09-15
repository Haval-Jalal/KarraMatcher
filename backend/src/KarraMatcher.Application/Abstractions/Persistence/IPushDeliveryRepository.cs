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
    /// Prenumerationerna som hör till bestämda konton — deras alla enheter (`#63`).
    ///
    /// <para>
    /// För en samåkningsnotis som ska nå en viss förälder: föraren vid en ny förfrågan, den
    /// som frågade vid ett svar. Ett konto utan registrerad enhet saknas helt enkelt i svaret,
    /// och får då ingen notis — appen visar ändå ändringen när hen öppnar den.
    /// </para>
    /// </summary>
    public Task<IReadOnlyList<PushTarget>> ListForAccountsAsync(
        IReadOnlyCollection<Guid> accountIds,
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

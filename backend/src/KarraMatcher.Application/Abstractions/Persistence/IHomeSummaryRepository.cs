using KarraMatcher.Domain.Events;

namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>
/// Läsningarna bakom Hem-vyn (`#307`-uppföljning). Varje metod är scopead till medlemmens egna
/// lag/kanaler/barn av anroparen; inget här avgör behörighet på egen hand.
/// </summary>
public interface IHomeSummaryRepository
{
    /// <summary>
    /// Närmaste kommande, ej inställda händelse i något av <paramref name="teamIds"/>. Laddar
    /// spelplats och klubb så att platsen kan lösas som i schemat.
    /// </summary>
    public Task<Event?> NextEventAsync(
        IReadOnlyCollection<Guid> teamIds,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Kommande händelser där ett av kontots egna barn saknar svar på en öppen kallelse (endast
    /// lag där kallelse är påslagen). En rad per händelse med antalet barn utan svar.
    /// </summary>
    public Task<IReadOnlyList<PendingKallelseRow>> PendingKallelserAsync(
        Guid accountId,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Det senaste publicerade, ej borttagna meddelandet i den kanal-uppsättning medlemmen ser:
    /// varje trupps primärkanal (<c>TeamId == null</c>) plus de lag hen når.
    /// </summary>
    public Task<ChatLatestRow?> LatestChatAsync(
        IReadOnlyCollection<Guid> truppIds,
        IReadOnlyCollection<Guid> teamIds,
        CancellationToken cancellationToken);
}

/// <summary>En obesvarad kallelse hopslagen per händelse (projektion, ingen entitet).</summary>
public sealed record PendingKallelseRow(
    Guid EventId,
    EventType Type,
    DateTime KickoffUtc,
    string? Title,
    string? Opponent,
    bool? IsHome,
    string TeamName,
    int UnansweredCount);

/// <summary>Det senaste meddelandet i en kanal (projektion). Kanalen är paret (trupp, ev. lag).</summary>
public sealed record ChatLatestRow(
    Guid AgeGroupId,
    Guid? TeamId,
    Guid AuthorAccountId,
    string Body,
    DateTime PublishAtUtc);

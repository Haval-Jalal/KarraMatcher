using KarraMatcher.Domain.Chat;

namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>En enskild anmälans motivering och tidpunkt (`#263`). Fritext, bara för admin (§KM.10).</summary>
public sealed record ReportReason(string Reason, DateTime CreatedUtc);

/// <summary>
/// Ett anmält meddelande med varje anmälans motivering — för adminens moderering (`#201`/`#263`).
/// </summary>
public sealed record ReportedMessageRow(
    Guid MessageId,
    Guid AuthorAccountId,
    string Body,
    DateTime? PublishedUtc,
    bool Deleted,
    IReadOnlyList<ReportReason> Reasons);

/// <summary>
/// Läser och skriver chattens meddelanden och anmälningar (§KM.1/§KM.10, `#201`).
/// </summary>
/// <summary>Ett lags chatt-kanal: laget och dess trupp, upplöst ur lagets slug (`#202`).</summary>
public sealed record TeamChannel(Guid TeamId, Guid AgeGroupId);

/// <summary>Ett meddelandes reaktioner per emoji: antal och om läsaren själv reagerat (`#301`).</summary>
public sealed record ReactionAggregate(Guid MessageId, string Emoji, int Count, bool Mine);

public interface IChatRepository
{
    public Task<bool> TruppExistsAsync(Guid ageGroupId, CancellationToken cancellationToken);

    /// <summary>Laget och dess trupp för en slug, eller null. Låter en lag-kanal delas per slug.</summary>
    public Task<TeamChannel?> FindTeamChannelAsync(string slug, CancellationToken cancellationToken);

    public Task AddMessageAsync(ChatMessage message, CancellationToken cancellationToken);

    /// <summary>Ett meddelande, spårat — för radering eller släpp. Null när det inte finns.</summary>
    public Task<ChatMessage?> FindMessageAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>De senaste publicerade meddelandena i kanalen, äldst först, högst <paramref name="limit"/>.</summary>
    public Task<IReadOnlyList<ChatMessage>> ListPublishedAsync(
        Guid ageGroupId, Guid? teamId, int limit, CancellationToken cancellationToken);

    /// <summary>Kontots egna schemalagda (ännu opublicerade) meddelanden i kanalen.</summary>
    public Task<IReadOnlyList<ChatMessage>> ListScheduledForAuthorAsync(
        Guid ageGroupId, Guid? teamId, Guid accountId, CancellationToken cancellationToken);

    /// <summary>Schemalagda meddelanden vars tid passerat, spårade — för släpp-jobbet.</summary>
    public Task<IReadOnlyList<ChatMessage>> ListDueForReleaseAsync(
        DateTime nowUtc, CancellationToken cancellationToken);

    public void RemoveMessage(ChatMessage message);

    /// <summary>Reaktionerna på en uppsättning meddelanden, per emoji, sett av <paramref name="readerAccountId"/> (`#301`).</summary>
    public Task<IReadOnlyList<ReactionAggregate>> ListReactionsAsync(
        IReadOnlyCollection<Guid> messageIds, Guid readerAccountId, CancellationToken cancellationToken);

    /// <summary>Kontots egen reaktion med en viss emoji, spårad — för att växla av. Null om ingen.</summary>
    public Task<ChatReaction?> FindReactionAsync(
        Guid messageId, Guid accountId, string emoji, CancellationToken cancellationToken);

    public Task AddReactionAsync(ChatReaction reaction, CancellationToken cancellationToken);

    public void RemoveReaction(ChatReaction reaction);

    public Task<bool> ReportExistsAsync(
        Guid messageId, Guid accountId, CancellationToken cancellationToken);

    public Task AddReportAsync(ChatReport report, CancellationToken cancellationToken);

    /// <summary>Antal anmälningar av ett meddelande — grinden för adminens radering (`#263`).</summary>
    public Task<int> ReportCountAsync(Guid messageId, CancellationToken cancellationToken);

    /// <summary>
    /// Anmälda meddelanden i hela truppen — trupp-kanalen och alla dess lag-kanaler — med
    /// antal anmälningar. Adminens kö (moderering delas mellan kanalerna, `#202`).
    /// </summary>
    public Task<IReadOnlyList<ReportedMessageRow>> ListReportedForTruppAsync(
        Guid ageGroupId, CancellationToken cancellationToken);

    /// <summary>Konton som stängt av Chatt för något lag i truppen ("av någonstans = av").</summary>
    public Task<IReadOnlyList<Guid>> ChatDisabledAccountIdsAsync(
        Guid ageGroupId, CancellationToken cancellationToken);

    /// <summary>Ett lag-id i truppen, för notisens per-lag-filter. Null om truppen saknar lag.</summary>
    public Task<Guid?> AnyTeamIdAsync(Guid ageGroupId, CancellationToken cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken);
}

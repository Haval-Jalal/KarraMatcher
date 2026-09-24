namespace KarraMatcher.Application.Features.Chat;

/// <summary>Ett publicerat meddelande så som chatten visar det (§KM.1/§KM.10, `#201`).</summary>
/// <param name="AuthorName">Den vuxnes visningsnamn, eller null. Aldrig ett barns namn.</param>
/// <param name="Body">Texten, eller tom när meddelandet är borttaget.</param>
/// <param name="Deleted">Sant när meddelandet tagits bort (visas som "[borttaget]").</param>
public sealed record ChatMessageDto(
    Guid Id,
    Guid AuthorAccountId,
    string? AuthorName,
    string Body,
    DateTimeOffset PublishedUtc,
    bool Deleted);

/// <summary>Ett schemalagt (ännu opublicerat) meddelande — den som skapade det ser sina.</summary>
public sealed record ScheduledMessageDto(Guid Id, string Body, DateTimeOffset PublishAtUtc);

/// <summary>
/// Meta om en lag-kanal som FE:t behöver: truppens id (för att avgöra admin via anspråk) och
/// om den inloggade är ledare (får schemalägga) (`#202`).
/// </summary>
public sealed record TeamChatMetaDto(Guid TruppId, bool IsLeader);

/// <summary>En anmälans motivering så som admin ser den (`#263`). Fritext, aldrig ett barns namn.</summary>
public sealed record ReportReasonDto(string Reason, DateTimeOffset ReportedUtc);

/// <summary>
/// Ett anmält meddelande i adminens moderering (`#201`/`#263`). <paramref name="Reasons"/> är
/// varje anmälares motivering; <paramref name="ReportCount"/> är antalet (grinden för radering).
/// </summary>
public sealed record ReportedMessageDto(
    Guid MessageId,
    Guid AuthorAccountId,
    string? AuthorName,
    string Body,
    bool Deleted,
    int ReportCount,
    IReadOnlyList<ReportReasonDto> Reasons);

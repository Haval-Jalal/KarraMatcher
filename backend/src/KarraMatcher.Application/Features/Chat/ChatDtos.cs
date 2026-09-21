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

/// <summary>Ett anmält meddelande i adminens moderering (`#201`).</summary>
public sealed record ReportedMessageDto(
    Guid MessageId,
    Guid AuthorAccountId,
    string? AuthorName,
    string Body,
    bool Deleted,
    int ReportCount);

namespace KarraMatcher.Application.Features.Chat;

/// <summary>Vilken sorts chatt-kanal en rad i kanallistan är (`#293`).</summary>
public enum ChatChannelKind
{
    /// <summary>Truppens primärkanal (paret utan lag).</summary>
    Trupp = 0,

    /// <summary>En lag-kanal.</summary>
    Team = 1,
}

/// <summary>
/// En chatt-kanal den inloggade får se i en trupp (`#293`). Kanalen är inget eget objekt utan
/// härleds ur paret (trupp, ev. lag): <see cref="Kind"/> <c>Trupp</c> har inget lag,
/// <c>Team</c> bär lagets id/slug/färg. Ett nytt lag ger en kanal direkt, utan skapa-åtgärd.
/// </summary>
/// <param name="TeamId">Lagets id, eller null för primärkanalen.</param>
/// <param name="Slug">Lagets slug (adressen <c>/lag/{slug}/chatt</c>), eller null för primärkanalen.</param>
/// <param name="Name">Visningsnamn: "{trupp} chatt" för primärkanalen, "Lag {färg} chatt" för ett lag.</param>
/// <param name="ColorHex">Lagets färg, eller null för primärkanalen.</param>
public sealed record ChatChannelDto(
    ChatChannelKind Kind,
    Guid? TeamId,
    string? Slug,
    string Name,
    string? ColorHex)
{
    /// <summary>Truppens primärkanal, namngiven efter truppen: t.ex. "P2016 chatt" (`#298`).</summary>
    public static ChatChannelDto Trupp(string truppName) =>
        new(ChatChannelKind.Trupp, null, null, $"{truppName} chatt", null);

    /// <summary>En lag-kanal, namngiven efter färgen: t.ex. "Lag Svart chatt" (`#298`).</summary>
    public static ChatChannelDto Team(Guid teamId, string slug, string colorName, string colorHex) =>
        new(ChatChannelKind.Team, teamId, slug, $"Lag {colorName} chatt", colorHex);
}

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

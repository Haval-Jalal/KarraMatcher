namespace KarraMatcher.Application.Features.Home;

/// <summary>
/// Hem-vyns sammanställning för den inloggade (`#307`-uppföljning, "allt samlat").
///
/// <para>
/// Aggregerar bara data medlemmen <b>ändå får se</b> — nästa händelse i något av hens lag,
/// kallelser som väntar på svar för hens egna barn, och det senaste i en kanal hen når. Ingen ny
/// datamodell, ingen ny PII; objektnivå-auktoriseringen bevaras genom att allt scopas till
/// kontots egna medlemskap server-side (§KM.1/§KM.3).
/// </para>
/// </summary>
public sealed record HomeSummaryDto(
    HomeEventDto? NextEvent,
    IReadOnlyList<HomePendingKallelseDto> PendingKallelser,
    HomeChatDto? LatestChat);

/// <summary>Nästa kommande händelse tvärs över medlemmens lag. Tiden i UTC (§KM.5).</summary>
public sealed record HomeEventDto(
    Guid Id,
    string Type,
    DateTimeOffset KickoffUtc,
    string? Title,
    string? Opponent,
    bool? IsHome,
    string TeamSlug,
    string TeamName,
    string Place);

/// <summary>
/// En kommande händelse där ett av medlemmens egna barn ännu inte svarat på kallelsen.
/// <paramref name="UnansweredCount"/> är antalet av hens barn utan svar — aldrig barnens namn.
/// </summary>
public sealed record HomePendingKallelseDto(
    Guid EventId,
    string Type,
    DateTimeOffset KickoffUtc,
    string? Title,
    string? Opponent,
    bool? IsHome,
    string TeamName,
    int UnansweredCount);

/// <summary>
/// Det senaste meddelandet i en kanal medlemmen ser. <paramref name="Snippet"/> är ett kort
/// utdrag ur fritexten (potentiell PII, loggas aldrig — §KM.10); avsändaren är en vuxens namn.
/// </summary>
public sealed record HomeChatDto(
    Guid TruppId,
    string? TeamSlug,
    string ChannelName,
    string AuthorName,
    string Snippet,
    DateTimeOffset SentAtUtc);

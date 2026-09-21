namespace KarraMatcher.Application.Features.Attendance;

/// <summary>
/// Vårdnadshavarens vy av en kallelse för en händelse (§KM.7, `#199`).
///
/// <para>Bara den inloggades egna kallade barn — visade som "Liam J" (§KM.1).</para>
/// </summary>
/// <param name="CallOpen">Sant när en kallelse öppnats för händelsen.</param>
/// <param name="KickoffUtc">Händelsens start i UTC; klienten visar svensk tid (§KM.5).</param>
/// <param name="Children">Den inloggades kallade barn och deras svar.</param>
public sealed record MyKallelseDto(
    bool CallOpen,
    DateTimeOffset KickoffUtc,
    IReadOnlyList<MyChildInvitationDto> Children);

/// <summary>Ett av den inloggades barn i en kallelse (§KM.7, `#199`).</summary>
/// <param name="Reply">"Coming", "NotComing" eller null (ej svarat).</param>
public sealed record MyChildInvitationDto(Guid ChildId, string DisplayName, string? Reply);

/// <summary>
/// Tränarens/adminens sammanställning av en kallelse (§KM.7, `#199`).
/// </summary>
/// <param name="CallOpen">Sant när en kallelse öppnats.</param>
/// <param name="Coming">Antal barn som svarat Ja.</param>
/// <param name="NotComing">Antal barn som svarat Nej.</param>
/// <param name="NotAnswered">Antal kallade barn utan svar.</param>
/// <param name="Children">De kallade barnen, med lag och svar.</param>
public sealed record KallelseSummaryDto(
    bool CallOpen,
    int Coming,
    int NotComing,
    int NotAnswered,
    IReadOnlyList<KallelseChildDto> Children);

/// <summary>Ett kallat barn i sammanställningen (§KM.7, §KM.1: "Liam J", aldrig hela efternamnet).</summary>
/// <param name="Reply">"Coming", "NotComing" eller null (ej svarat).</param>
public sealed record KallelseChildDto(
    Guid ChildId,
    string DisplayName,
    string? TeamName,
    string? ColorHex,
    string? Reply);

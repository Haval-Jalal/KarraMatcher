namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>En trupp den inloggade är medlem av — för medlemmens egna vyer (t.ex. chatt, `#201`).</summary>
/// <param name="IsLeader">Sant om kontot är admin för truppen eller tränare för något av dess lag (får schemalägga).</param>
public sealed record MemberTruppDto(Guid Id, string ClubName, string Name, string Season, bool IsLeader);

/// <summary>
/// Avgör om ett konto är <b>medlem</b> och därmed får se en trupps eller ett lags innehåll
/// (v2, `#191`). Appen är stängd (§KM.3): inloggning räcker inte, man måste höra till.
///
/// <para>
/// Medlem av ett lag är den som: är <em>admin</em> för lagets trupp, <em>tränare</em> för
/// laget, eller <em>vårdnadshavare</em> till ett barn i laget. Superadmin ser allt och
/// kortsluts redan i auktoriseringslagret via sitt anspråk — den kontrollen behöver därför
/// inte gå hit.
/// </para>
/// </summary>
public interface IMembershipService
{
    /// <summary>Är kontot medlem av laget (via admin, tränare eller vårdnadshavare)?</summary>
    public Task<bool> IsMemberOfTeamAsync(
        Guid accountId,
        Guid teamId,
        CancellationToken cancellationToken);

    /// <summary>Trupperna den inloggade är medlem av — för medlemmens egna vyer (`#201`).</summary>
    public Task<IReadOnlyList<MemberTruppDto>> MemberTrupperAsync(
        Guid accountId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Är kontot en <em>ledare</em> i truppen — admin för truppen eller tränare för något av
    /// dess lag (eller superadmin)? Ledare får schemalägga chatt-meddelanden (`#201`).
    /// </summary>
    public Task<bool> IsLeaderOfTruppAsync(
        Guid accountId,
        Guid ageGroupId,
        CancellationToken cancellationToken);

    /// <summary>Är kontot medlem av truppen (åldersgruppen) — via valfritt av dess lag eller trupp-roll?</summary>
    public Task<bool> IsMemberOfTruppAsync(
        Guid accountId,
        Guid ageGroupId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Konto-id för alla medlemmar av truppen (tvärs över dess lag) — för att rikta en
    /// trupp-notis (`#201`). Global superadmin räknas inte som medlem här.
    /// </summary>
    public Task<IReadOnlyList<Guid>> MemberAccountIdsForTruppAsync(
        Guid ageGroupId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Konto-id för <em>alla</em> medlemmar av laget — motsatt riktning mot de per-konto-frågor
    /// resten av interfacet ställer (`#200`). Används för att rikta en lag-notis till medlemmar
    /// i stället för till öppna prenumeranter (§KM.3). Global superadmin räknas inte som
    /// lag-medlem här: en lag-notis ska inte nå plattformsägaren för varje lag.
    /// </summary>
    public Task<IReadOnlyList<Guid>> MemberAccountIdsAsync(
        Guid teamId,
        CancellationToken cancellationToken);

    /// <summary>Samma, men laget anges med sin slug — som i adressen <c>/lag/{slug}</c>.</summary>
    public Task<bool> IsMemberOfTeamBySlugAsync(
        Guid accountId,
        string teamSlug,
        CancellationToken cancellationToken);

    /// <summary>Är kontot medlem av händelsens lag?</summary>
    public Task<bool> IsMemberOfEventAsync(
        Guid accountId,
        Guid eventId,
        CancellationToken cancellationToken);

    /// <summary>Lag-slugarna kontot är medlem av. Används för att lista bara det man får se.</summary>
    public Task<IReadOnlyList<string>> MemberTeamSlugsAsync(
        Guid accountId,
        CancellationToken cancellationToken);
}

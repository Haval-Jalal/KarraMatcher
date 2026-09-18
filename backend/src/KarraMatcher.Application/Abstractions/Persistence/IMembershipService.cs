namespace KarraMatcher.Application.Abstractions.Persistence;

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

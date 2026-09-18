using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Teams;

namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>
/// Skrivåtkomst till plattformens struktur — superadmins verktyg (§KM.3, `#192`).
///
/// <para>
/// En egen, samlad ingång för superadmin-modulen: sporter, klubbar, trupper, lag och
/// admin-tilldelning. Skild från läsvägarnas repositorier, så att den breda skrivytan aldrig
/// kan nås av en publik läsning.
/// </para>
/// </summary>
public interface IAdministrationRepository
{
    // ---- Sport -----------------------------------------------------------------------
    public Task<IReadOnlyList<Sport>> GetSportsAsync(CancellationToken cancellationToken);

    public Task<Sport?> FindSportAsync(Guid id, CancellationToken cancellationToken);

    public Task<bool> SportSlugExistsAsync(string slug, CancellationToken cancellationToken);

    public Task AddSportAsync(Sport sport, CancellationToken cancellationToken);

    // ---- Klubb -----------------------------------------------------------------------
    public Task<IReadOnlyList<Club>> GetClubsAsync(CancellationToken cancellationToken);

    public Task<Club?> FindClubAsync(Guid id, CancellationToken cancellationToken);

    public Task<bool> ClubSlugExistsAsync(string slug, CancellationToken cancellationToken);

    public Task<bool> ClubExistsAsync(Guid id, CancellationToken cancellationToken);

    public Task AddClubAsync(Club club, CancellationToken cancellationToken);

    // ---- Trupp (AgeGroup) ------------------------------------------------------------
    public Task<IReadOnlyList<AgeGroup>> GetTrupperAsync(Guid? clubId, CancellationToken cancellationToken);

    /// <summary>Trupperna en admin faktiskt sköter (`#193`) — alla för en superadmin.</summary>
    public Task<IReadOnlyList<AgeGroup>> GetTrupperForAdminAsync(
        Guid accountId, bool isSuperAdmin, CancellationToken cancellationToken);

    public Task<AgeGroup?> FindTruppAsync(Guid id, CancellationToken cancellationToken);

    public Task<bool> TruppExistsAsync(Guid id, CancellationToken cancellationToken);

    public Task<bool> SportExistsAsync(Guid id, CancellationToken cancellationToken);

    public Task<bool> TruppNameTakenAsync(
        Guid clubId, string name, string season, Guid? excludingId, CancellationToken cancellationToken);

    public Task AddTruppAsync(AgeGroup trupp, CancellationToken cancellationToken);

    // ---- Lag (Team) ------------------------------------------------------------------
    public Task<IReadOnlyList<Team>> GetLagAsync(Guid truppId, CancellationToken cancellationToken);

    public Task<Team?> FindLagAsync(Guid id, CancellationToken cancellationToken);

    public Task<bool> LagSlugExistsAsync(string slug, CancellationToken cancellationToken);

    public Task AddLagAsync(Team lag, CancellationToken cancellationToken);

    // ---- Admin-tilldelning -----------------------------------------------------------
    public Task<Account?> FindAccountByEmailAsync(string email, CancellationToken cancellationToken);

    public Task<TeamRole?> FindAdminRoleAsync(
        Guid accountId, Guid truppId, CancellationToken cancellationToken);

    public Task<IReadOnlyList<TeamRole>> GetAdminsAsync(Guid truppId, CancellationToken cancellationToken);

    // ---- Tränartillsättning (`#197`) -------------------------------------------------
    public Task<TeamRole?> FindCoachRoleAsync(
        Guid accountId, Guid teamId, CancellationToken cancellationToken);

    /// <summary>Alla tränarroller i truppen (lag vars trupp är <paramref name="truppId"/>).</summary>
    public Task<IReadOnlyList<TeamRole>> GetCoachesForTruppAsync(
        Guid truppId, CancellationToken cancellationToken);

    public Task AddRoleAsync(TeamRole role, CancellationToken cancellationToken);

    public void RemoveRole(TeamRole role);

    public Task SaveChangesAsync(CancellationToken cancellationToken);
}

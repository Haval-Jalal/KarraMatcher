using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Children;
using KarraMatcher.Domain.Teams;

namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>
/// Skrivåtkomst till barn och deras vårdnadshavarkopplingar (§KM.1, `#196`).
///
/// <para>
/// Barnprofilen är minimal (förnamn + efternamnsinitial). Att koppla en vårdnadshavare
/// kräver samtycke (§KM.6) — det prövas i tjänsten, inte här.
/// </para>
/// </summary>
public interface IChildRepository
{
    // ---- Barn ------------------------------------------------------------------------
    public Task AddAsync(Child child, CancellationToken cancellationToken);

    /// <summary>Barnet med lag och vårdnadshavare inlästa, spårat för ändring.</summary>
    public Task<Child?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    public void Remove(Child child);

    public Task<bool> TruppExistsAsync(Guid ageGroupId, CancellationToken cancellationToken);

    public Task<bool> TeamInTruppAsync(
        Guid teamId, Guid ageGroupId, CancellationToken cancellationToken);

    // ---- Överblick -------------------------------------------------------------------
    public Task<IReadOnlyList<Team>> GetTeamsForTruppAsync(
        Guid ageGroupId, CancellationToken cancellationToken);

    /// <summary>Truppens barn med lag inlästa.</summary>
    public Task<IReadOnlyList<Child>> GetChildrenForTruppAsync(
        Guid ageGroupId, CancellationToken cancellationToken);

    /// <summary>Vårdnadshavarkopplingarna för truppens barn, med konton — för överblicken.</summary>
    public Task<IReadOnlyList<Guardianship>> GetGuardianshipsForTruppAsync(
        Guid ageGroupId, CancellationToken cancellationToken);

    // ---- Vårdnadshavare --------------------------------------------------------------
    public Task<Account?> FindAccountByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>Sant om kontot gått med i truppen (accepterad inbjudan eller godkänd ansökan).</summary>
    public Task<bool> HasJoinedTruppAsync(
        Guid accountId, Guid ageGroupId, CancellationToken cancellationToken);

    public Task<bool> GuardianshipExistsAsync(
        Guid accountId, Guid childId, CancellationToken cancellationToken);

    public Task<Guardianship?> FindGuardianshipAsync(
        Guid accountId, Guid childId, CancellationToken cancellationToken);

    public Task AddGuardianshipAsync(Guardianship guardianship, CancellationToken cancellationToken);

    public void RemoveGuardianship(Guardianship guardianship);

    public Task SaveChangesAsync(CancellationToken cancellationToken);
}

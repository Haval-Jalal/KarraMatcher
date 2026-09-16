using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Invitations;

namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>
/// Skrivåtkomst till inbjudningar (`#193`). En accepterad inbjudan är också ett medlemskap,
/// så det som ändras läses spårat — läsvägarna (medlemskapskollen) sitter i
/// <see cref="IMembershipService"/>.
/// </summary>
public interface IInvitationRepository
{
    public Task AddAsync(Invitation invitation, CancellationToken cancellationToken);

    /// <summary>Slår upp på token-hashen, spårat, med truppen och laget inlästa.</summary>
    public Task<Invitation?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    public Task<Invitation?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    public Task<IReadOnlyList<Invitation>> GetPendingForTruppAsync(
        Guid ageGroupId, CancellationToken cancellationToken);

    /// <summary>Kontot bakom ett anrop, för att jämföra dess adress med inbjudans.</summary>
    public Task<Account?> FindAccountAsync(Guid accountId, CancellationToken cancellationToken);

    public Task<bool> TruppExistsAsync(Guid ageGroupId, CancellationToken cancellationToken);

    public Task<bool> TeamInTruppAsync(
        Guid teamId, Guid ageGroupId, CancellationToken cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken);
}

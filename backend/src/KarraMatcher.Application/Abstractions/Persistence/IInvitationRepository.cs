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

    /// <summary>
    /// Tar bort alla inbjudningar adresserade till <paramref name="email"/>, precis som
    /// <see cref="ILoginCodeRepository.DeleteForEmailAsync"/> gör för koderna.
    ///
    /// <para>
    /// En inbjudan är bunden till en adress, inte till ett konto: en <em>accepterad</em>
    /// inbjudan kaskaderar bort med kontot via <c>AcceptedByAccountId</c>, men en väntande,
    /// utgången eller återkallad gör det inte — dess <c>Email</c> ligger kvar i klartext
    /// efter att personen raderat sitt konto (§KM.6). Den tas därför bort uttryckligen här.
    /// Inbjudningar kontot <em>skapat</em> åt andra rörs inte: de bär bara ett id
    /// (<c>CreatedByAccountId</c>, §KM.10), aldrig den raderades namn eller adress.
    /// </para>
    /// </summary>
    public Task DeleteForEmailAsync(string email, CancellationToken cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken);
}

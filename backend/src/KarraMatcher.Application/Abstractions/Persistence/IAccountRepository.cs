using KarraMatcher.Domain.Accounts;

namespace KarraMatcher.Application.Abstractions.Persistence;

public interface IAccountRepository
{
    public Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Adressen jämförs normaliserad — se <see cref="Account.Email"/>.</summary>
    public Task<Account?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Visningsnamnen för flera konton på en gång.
    ///
    /// <para>
    /// Finns för samåkningen, som ska kunna säga "Anna kör" utan att ställa en fråga per
    /// rad. Konton utan ifyllt namn saknas i svaret — de har inget att visa, och en tom
    /// sträng hade sett ut som ett namn.
    /// </para>
    /// </summary>
    public Task<IReadOnlyDictionary<Guid, string>> DisplayNamesAsync(
        IReadOnlyCollection<Guid> accountIds,
        CancellationToken cancellationToken);

    public Task AddAsync(Account account, CancellationToken cancellationToken);

    /// <summary>
    /// Tar bort kontot. Raderar på riktigt, inte som en markering (§KM.6) — och tar med
    /// sig det som kaskaderar från det.
    /// </summary>
    public void Remove(Account account);

    public Task SaveChangesAsync(CancellationToken cancellationToken);
}

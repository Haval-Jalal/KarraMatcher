using KarraMatcher.Domain.Accounts;

namespace KarraMatcher.Application.Abstractions.Persistence;

public interface IAccountRepository
{
    public Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Adressen jämförs normaliserad — se <see cref="Account.Email"/>.</summary>
    public Task<Account?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    public Task AddAsync(Account account, CancellationToken cancellationToken);

    /// <summary>
    /// Tar bort kontot. Raderar på riktigt, inte som en markering (§KM.6) — och tar med
    /// sig det som kaskaderar från det.
    /// </summary>
    public void Remove(Account account);

    public Task SaveChangesAsync(CancellationToken cancellationToken);
}

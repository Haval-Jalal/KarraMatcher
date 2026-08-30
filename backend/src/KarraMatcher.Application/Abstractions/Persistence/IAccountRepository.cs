using KarraMatcher.Domain.Accounts;

namespace KarraMatcher.Application.Abstractions.Persistence;

public interface IAccountRepository
{
    public Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Adressen jämförs normaliserad — se <see cref="Account.Email"/>.</summary>
    public Task<Account?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    public Task AddAsync(Account account, CancellationToken cancellationToken);
}

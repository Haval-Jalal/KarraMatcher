using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Accounts;

namespace KarraMatcher.Application.Tests;

/// <summary>Delas av inloggnings- och raderingstesterna.</summary>
internal sealed class FakeAccountRepository : IAccountRepository
{
    public List<Account> Accounts { get; } = [];

    public Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Accounts.FirstOrDefault(a => a.Id == id));

    public Task<Account?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult(Accounts.FirstOrDefault(a => a.Email == email));

    public Task<IReadOnlyDictionary<Guid, string>> DisplayNamesAsync(
        IReadOnlyCollection<Guid> accountIds,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, string>>(
            Accounts
                .Where(a => accountIds.Contains(a.Id) && a.DisplayName is not null)
                .ToDictionary(a => a.Id, a => a.DisplayName!));

    public Task AddAsync(Account account, CancellationToken cancellationToken)
    {
        Accounts.Add(account);

        return Task.CompletedTask;
    }

    public void Remove(Account account) => Accounts.Remove(account);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

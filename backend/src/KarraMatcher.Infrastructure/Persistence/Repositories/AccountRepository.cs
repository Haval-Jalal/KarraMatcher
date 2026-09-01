using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Accounts;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

internal sealed class AccountRepository(KarraMatcherDbContext context) : IAccountRepository
{
    public Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Accounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<Account?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);

        // Normaliseras här också, inte bara vid skrivning: en anropare som skickar in
        // versaler ska hitta kontot, inte skapa ett andra.
        var normalized = email.Trim().ToLowerInvariant();

        return context.Accounts.FirstOrDefaultAsync(a => a.Email == normalized, cancellationToken);
    }

    public async Task AddAsync(Account account, CancellationToken cancellationToken) =>
        await context.Accounts.AddAsync(account, cancellationToken).ConfigureAwait(false);

    public void Remove(Account account) => context.Accounts.Remove(account);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}

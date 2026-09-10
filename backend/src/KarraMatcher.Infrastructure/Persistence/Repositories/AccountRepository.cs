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

    public async Task<IReadOnlyDictionary<Guid, string>> DisplayNamesAsync(
        IReadOnlyCollection<Guid> accountIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountIds);

        if (accountIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        /*
         * Bara id och namn hamtas -- inte hela kontot, och alltsa inte adressen. Ett
         * samakningssvar som rakade fa med sig en mejladress hade lackt den till hela laget.
         */
        var rows = await context.Accounts
            .AsNoTracking()
            .Where(a => accountIds.Contains(a.Id) && a.FirstName != null && a.FirstName != "")
            .Select(a => new { a.Id, a.FirstName, a.LastName })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ToDictionary(
            row => row.Id,
            row => string.IsNullOrWhiteSpace(row.LastName)
                ? row.FirstName!
                : $"{row.FirstName} {row.LastName}");
    }

    public async Task AddAsync(Account account, CancellationToken cancellationToken) =>
        await context.Accounts.AddAsync(account, cancellationToken).ConfigureAwait(false);

    public void Remove(Account account) => context.Accounts.Remove(account);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}

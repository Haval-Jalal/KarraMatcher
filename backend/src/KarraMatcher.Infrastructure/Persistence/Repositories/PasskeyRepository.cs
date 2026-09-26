using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Accounts;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

/// <summary>Passkeys per konto (§KM.6-kaskad). Uppslag på credential-id och per konto.</summary>
internal sealed class PasskeyRepository(KarraMatcherDbContext context) : IPasskeyRepository
{
    public async Task<Passkey?> FindByCredentialIdAsync(
        byte[] credentialId,
        CancellationToken cancellationToken) =>
        // Spårad: inloggningen räknar upp signeringsräknaren och sparar den på samma rad.
        await context.Passkeys
            .FirstOrDefaultAsync(p => p.CredentialId == credentialId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<Passkey?> FindForAccountAsync(
        Guid accountId,
        Guid id,
        CancellationToken cancellationToken) =>
        await context.Passkeys
            .FirstOrDefaultAsync(p => p.Id == id && p.AccountId == accountId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Passkey>> ListForAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken) =>
        await context.Passkeys
            .AsNoTracking()
            .Where(p => p.AccountId == accountId)
            .OrderBy(p => p.CreatedUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<byte[]>> CredentialIdsForAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken) =>
        await context.Passkeys
            .AsNoTracking()
            .Where(p => p.AccountId == accountId)
            .Select(p => p.CredentialId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<bool> ExistsAsync(byte[] credentialId, CancellationToken cancellationToken) =>
        await context.Passkeys
            .AsNoTracking()
            .AnyAsync(p => p.CredentialId == credentialId, cancellationToken)
            .ConfigureAwait(false);

    public async Task AddAsync(Passkey passkey, CancellationToken cancellationToken) =>
        await context.Passkeys.AddAsync(passkey, cancellationToken).ConfigureAwait(false);

    public void Remove(Passkey passkey) => context.Passkeys.Remove(passkey);

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
}

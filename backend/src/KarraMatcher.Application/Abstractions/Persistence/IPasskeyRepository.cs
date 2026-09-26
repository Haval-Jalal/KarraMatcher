using KarraMatcher.Domain.Accounts;

namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>Passkeys (WebAuthn-credentials) per konto. En passkey ägs av kontot och kaskaderar (§KM.6).</summary>
public interface IPasskeyRepository
{
    public Task<Passkey?> FindByCredentialIdAsync(byte[] credentialId, CancellationToken cancellationToken);

    /// <summary>En passkey som hör till <em>det här</em> kontot — objektnivå-auktorisering vid radering.</summary>
    public Task<Passkey?> FindForAccountAsync(Guid accountId, Guid id, CancellationToken cancellationToken);

    public Task<IReadOnlyList<Passkey>> ListForAccountAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>Kontots credential-id:n, för att inte registrera samma enhet två gånger.</summary>
    public Task<IReadOnlyList<byte[]>> CredentialIdsForAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken);

    public Task<bool> ExistsAsync(byte[] credentialId, CancellationToken cancellationToken);

    public Task AddAsync(Passkey passkey, CancellationToken cancellationToken);

    public void Remove(Passkey passkey);

    public Task SaveChangesAsync(CancellationToken cancellationToken);
}

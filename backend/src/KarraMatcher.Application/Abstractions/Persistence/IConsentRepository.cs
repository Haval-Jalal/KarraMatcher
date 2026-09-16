using KarraMatcher.Domain.Consent;

namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>Vårdnadshavarsamtycken (§KM.6, `#195`).</summary>
public interface IConsentRepository
{
    public Task AddAsync(GuardianConsent consent, CancellationToken cancellationToken);

    /// <summary>Sant om kontot redan samtyckt till en viss version.</summary>
    public Task<bool> HasVersionAsync(
        Guid accountId, string version, CancellationToken cancellationToken);

    /// <summary>Kontots senaste samtycke, oavsett version — för att visa vad man samtyckte till.</summary>
    public Task<GuardianConsent?> GetLatestForAsync(
        Guid accountId, CancellationToken cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken);
}

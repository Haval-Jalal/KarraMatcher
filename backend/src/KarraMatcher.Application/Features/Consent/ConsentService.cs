using KarraMatcher.Application.Abstractions.Audit;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Administration;
using KarraMatcher.Domain.Audit;
using KarraMatcher.Domain.Consent;

namespace KarraMatcher.Application.Features.Consent;

/// <summary>
/// Vårdnadshavarsamtyckets livscykel (§KM.6, `#195`): visa texten, ta emot samtycke, och
/// svara på vad någon samtyckt till.
///
/// <para>
/// Per vårdnadshavare: en vuxen samtycker till den aktuella versionen, och det låser upp
/// koppling av deras barn (`#196` kollar <see cref="HasCurrentConsentAsync"/>). Version och
/// tidsstämpel sparas; samtycket audit-loggas med kontots id, aldrig något om barnet.
/// </para>
/// </summary>
public sealed class ConsentService(
    IConsentRepository consents,
    IAuditLog audit,
    TimeProvider clock)
{
    public async Task<AdminOutcome> GrantAsync(
        Guid accountId, string version, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);

        // Bara den aktuella versionen går att samtycka till. En stale version betyder att
        // texten hunnit uppdateras sedan sidan lästes in — läs den nya först.
        if (!string.Equals(version, ConsentDocument.CurrentVersion, StringComparison.Ordinal))
        {
            return AdminOutcome.Conflict;
        }

        // Idempotent: har man redan samtyckt till versionen händer inget nytt.
        if (await consents.HasVersionAsync(accountId, version, cancellationToken).ConfigureAwait(false))
        {
            return AdminOutcome.Success;
        }

        var consent = new GuardianConsent
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Version = version,
            GrantedUtc = clock.GetUtcNow().UtcDateTime,
        };

        await consents.AddAsync(consent, cancellationToken).ConfigureAwait(false);
        await audit.RecordAsync(AuditActions.ConsentGranted, accountId, cancellationToken, accountId)
            .ConfigureAwait(false);
        await consents.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return AdminOutcome.Success;
    }

    public async Task<MyConsentDto> MyConsentAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var latest = await consents.GetLatestForAsync(accountId, cancellationToken)
            .ConfigureAwait(false);

        if (latest is null)
        {
            return new MyConsentDto(false, null, null, null);
        }

        return new MyConsentDto(
            string.Equals(latest.Version, ConsentDocument.CurrentVersion, StringComparison.Ordinal),
            latest.Version,
            new DateTimeOffset(latest.GrantedUtc, TimeSpan.Zero),
            ConsentDocument.TextFor(latest.Version));
    }

    /// <summary>Har kontot samtyckt till den aktuella versionen? Används av `#196` före koppling.</summary>
    public Task<bool> HasCurrentConsentAsync(Guid accountId, CancellationToken cancellationToken) =>
        consents.HasVersionAsync(accountId, ConsentDocument.CurrentVersion, cancellationToken);
}

using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Consent;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

internal sealed class ConsentRepository(KarraMatcherDbContext context) : IConsentRepository
{
    public async Task AddAsync(GuardianConsent consent, CancellationToken cancellationToken) =>
        await context.GuardianConsents.AddAsync(consent, cancellationToken).ConfigureAwait(false);

    public Task<bool> HasVersionAsync(
        Guid accountId, string version, CancellationToken cancellationToken) =>
        context.GuardianConsents.AsNoTracking()
            .AnyAsync(c => c.AccountId == accountId && c.Version == version, cancellationToken);

    public Task<GuardianConsent?> GetLatestForAsync(
        Guid accountId, CancellationToken cancellationToken) =>
        context.GuardianConsents.AsNoTracking()
            .Where(c => c.AccountId == accountId)
            .OrderByDescending(c => c.GrantedUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}

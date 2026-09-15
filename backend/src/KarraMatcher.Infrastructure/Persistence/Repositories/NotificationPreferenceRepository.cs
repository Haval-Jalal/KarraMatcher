using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Push;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

internal sealed class NotificationPreferenceRepository(KarraMatcherDbContext context)
    : INotificationPreferenceRepository
{
    public Task<NotificationPreference?> FindAsync(
        Guid accountId,
        Guid teamId,
        CancellationToken cancellationToken) =>
        // Spårad -- det här är läsningen inför en ändring.
        context.NotificationPreferences
            .FirstOrDefaultAsync(p => p.AccountId == accountId && p.TeamId == teamId, cancellationToken);

    public async Task AddAsync(
        NotificationPreference preference,
        CancellationToken cancellationToken) =>
        await context.NotificationPreferences.AddAsync(preference, cancellationToken)
            .ConfigureAwait(false);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}

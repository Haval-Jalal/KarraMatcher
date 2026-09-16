using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Invitations;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

internal sealed class InvitationRepository(KarraMatcherDbContext context) : IInvitationRepository
{
    public async Task AddAsync(Invitation invitation, CancellationToken cancellationToken) =>
        await context.Invitations.AddAsync(invitation, cancellationToken).ConfigureAwait(false);

    public Task<Invitation?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        context.Invitations
            .Include(i => i.AgeGroup)
            .Include(i => i.Team)
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);

    public Task<Invitation?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Invitations.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Invitation>> GetPendingForTruppAsync(
        Guid ageGroupId, CancellationToken cancellationToken) =>
        await context.Invitations.AsNoTracking()
            .Include(i => i.Team)
            .Where(i => i.AgeGroupId == ageGroupId && i.Status == InvitationStatus.Pending)
            .OrderByDescending(i => i.CreatedUtc)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<Account?> FindAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
        context.Accounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);

    public Task<bool> TruppExistsAsync(Guid ageGroupId, CancellationToken cancellationToken) =>
        context.AgeGroups.AsNoTracking().AnyAsync(a => a.Id == ageGroupId, cancellationToken);

    public Task<bool> TeamInTruppAsync(
        Guid teamId, Guid ageGroupId, CancellationToken cancellationToken) =>
        context.Teams.AsNoTracking()
            .AnyAsync(t => t.Id == teamId && t.AgeGroupId == ageGroupId, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}

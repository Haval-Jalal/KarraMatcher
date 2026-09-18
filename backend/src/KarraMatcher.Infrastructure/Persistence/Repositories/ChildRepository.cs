using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Applications;
using KarraMatcher.Domain.Children;
using KarraMatcher.Domain.Invitations;
using KarraMatcher.Domain.Teams;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

internal sealed class ChildRepository(KarraMatcherDbContext context) : IChildRepository
{
    public async Task AddAsync(Child child, CancellationToken cancellationToken) =>
        await context.Children.AddAsync(child, cancellationToken).ConfigureAwait(false);

    public Task<Child?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Children
            .Include(c => c.Team)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public void Remove(Child child) => context.Children.Remove(child);

    public Task<bool> TruppExistsAsync(Guid ageGroupId, CancellationToken cancellationToken) =>
        context.AgeGroups.AsNoTracking().AnyAsync(a => a.Id == ageGroupId, cancellationToken);

    public Task<bool> TeamInTruppAsync(
        Guid teamId, Guid ageGroupId, CancellationToken cancellationToken) =>
        context.Teams.AsNoTracking()
            .AnyAsync(t => t.Id == teamId && t.AgeGroupId == ageGroupId, cancellationToken);

    public async Task<IReadOnlyList<Team>> GetTeamsForTruppAsync(
        Guid ageGroupId, CancellationToken cancellationToken) =>
        await context.Teams.AsNoTracking()
            .Where(t => t.AgeGroupId == ageGroupId)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<Child>> GetChildrenForTruppAsync(
        Guid ageGroupId, CancellationToken cancellationToken) =>
        await context.Children.AsNoTracking()
            .Include(c => c.Team)
            .Where(c => c.AgeGroupId == ageGroupId)
            .OrderBy(c => c.FirstName).ThenBy(c => c.LastInitial)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<Guardianship>> GetGuardianshipsForTruppAsync(
        Guid ageGroupId, CancellationToken cancellationToken) =>
        await context.Guardianships.AsNoTracking()
            .Include(g => g.Account)
            .Where(g => g.Child!.AgeGroupId == ageGroupId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<Account?> FindAccountByEmailAsync(string email, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);

        var normalized = email.Trim().ToLowerInvariant();

        return context.Accounts.FirstOrDefaultAsync(a => a.Email == normalized, cancellationToken);
    }

    public async Task<bool> HasJoinedTruppAsync(
        Guid accountId, Guid ageGroupId, CancellationToken cancellationToken)
    {
        var invited = await context.Invitations.AsNoTracking().AnyAsync(
            i => i.AcceptedByAccountId == accountId
                && i.AgeGroupId == ageGroupId
                && i.Status == InvitationStatus.Accepted,
            cancellationToken).ConfigureAwait(false);

        if (invited)
        {
            return true;
        }

        return await context.MembershipApplications.AsNoTracking().AnyAsync(
            a => a.AccountId == accountId
                && a.AgeGroupId == ageGroupId
                && a.Status == ApplicationStatus.Approved,
            cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> GuardianshipExistsAsync(
        Guid accountId, Guid childId, CancellationToken cancellationToken) =>
        context.Guardianships.AsNoTracking()
            .AnyAsync(g => g.AccountId == accountId && g.ChildId == childId, cancellationToken);

    public Task<Guardianship?> FindGuardianshipAsync(
        Guid accountId, Guid childId, CancellationToken cancellationToken) =>
        context.Guardianships
            .FirstOrDefaultAsync(g => g.AccountId == accountId && g.ChildId == childId, cancellationToken);

    public async Task AddGuardianshipAsync(
        Guardianship guardianship, CancellationToken cancellationToken) =>
        await context.Guardianships.AddAsync(guardianship, cancellationToken).ConfigureAwait(false);

    public void RemoveGuardianship(Guardianship guardianship) =>
        context.Guardianships.Remove(guardianship);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}

using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Applications;
using KarraMatcher.Domain.Teams;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

internal sealed class ApplicationRepository(KarraMatcherDbContext context) : IApplicationRepository
{
    public async Task AddAsync(MembershipApplication application, CancellationToken cancellationToken) =>
        await context.MembershipApplications.AddAsync(application, cancellationToken)
            .ConfigureAwait(false);

    public Task<MembershipApplication?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.MembershipApplications.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<IReadOnlyList<MembershipApplication>> GetPendingForTruppAsync(
        Guid ageGroupId, CancellationToken cancellationToken) =>
        await context.MembershipApplications.AsNoTracking()
            .Include(a => a.Account)
            .Where(a => a.AgeGroupId == ageGroupId && a.Status == ApplicationStatus.Pending)
            .OrderBy(a => a.CreatedUtc)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<bool> HasOpenForAsync(
        Guid accountId, Guid ageGroupId, CancellationToken cancellationToken) =>
        context.MembershipApplications.AsNoTracking().AnyAsync(
            a => a.AccountId == accountId
                && a.AgeGroupId == ageGroupId
                && (a.Status == ApplicationStatus.Pending || a.Status == ApplicationStatus.Approved),
            cancellationToken);

    public Task<AgeGroup?> FindTruppAsync(Guid id, CancellationToken cancellationToken) =>
        context.AgeGroups.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<bool> TruppExistsAsync(Guid id, CancellationToken cancellationToken) =>
        context.AgeGroups.AsNoTracking().AnyAsync(a => a.Id == id, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}

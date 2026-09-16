using KarraMatcher.Domain.Applications;
using KarraMatcher.Domain.Teams;

namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>
/// Skrivåtkomst till medlemskapsansökningar (`#194`). En godkänd ansökan är också ett
/// medlemskap — medlemskapskollen sitter i <see cref="IMembershipService"/>.
/// </summary>
public interface IApplicationRepository
{
    public Task AddAsync(MembershipApplication application, CancellationToken cancellationToken);

    public Task<MembershipApplication?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    public Task<IReadOnlyList<MembershipApplication>> GetPendingForTruppAsync(
        Guid ageGroupId, CancellationToken cancellationToken);

    /// <summary>Sant om kontot redan har en väntande eller godkänd ansökan till truppen.</summary>
    public Task<bool> HasOpenForAsync(
        Guid accountId, Guid ageGroupId, CancellationToken cancellationToken);

    public Task<AgeGroup?> FindTruppAsync(Guid id, CancellationToken cancellationToken);

    public Task<bool> TruppExistsAsync(Guid id, CancellationToken cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken);
}

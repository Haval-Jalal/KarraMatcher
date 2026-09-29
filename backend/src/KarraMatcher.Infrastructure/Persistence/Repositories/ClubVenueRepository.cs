using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Teams;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

internal sealed class ClubVenueRepository(KarraMatcherDbContext context) : IClubVenueRepository
{
    public Task<AgeGroup?> FindTruppAsync(Guid truppId, CancellationToken cancellationToken) =>
        context.AgeGroups.FirstOrDefaultAsync(ageGroup => ageGroup.Id == truppId, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}

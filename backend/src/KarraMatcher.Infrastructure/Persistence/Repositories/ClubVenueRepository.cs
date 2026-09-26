using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Teams;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

internal sealed class ClubVenueRepository(KarraMatcherDbContext context) : IClubVenueRepository
{
    public Task<Club?> FindClubByTruppAsync(Guid truppId, CancellationToken cancellationToken) =>
        context.Clubs.FirstOrDefaultAsync(
            club => club.AgeGroups.Any(ageGroup => ageGroup.Id == truppId),
            cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}

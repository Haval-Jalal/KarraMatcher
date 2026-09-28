using KarraMatcher.Application.Abstractions.Persistence;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

internal sealed class AttendanceRepository(KarraMatcherDbContext context) : IAttendanceRepository
{
    public async Task<bool?> IsEnabledForTeamAsync(string slug, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(slug);

        // Bara flaggan hamtas. Grinden ska vara billig -- den star framfor varje anrop till
        // kallelsen, och en fraga som drar hem hela laget hade betalats vid varje sidvisning.
        var found = await context.Teams
            .AsNoTracking()
            .Where(team => team.Slug == slug)
            .Select(team => (bool?)team.AttendanceEnabled)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return found;
    }

    public async Task<bool?> IsEnabledForMatchAsync(Guid matchId, CancellationToken cancellationToken) =>
        await context.Events
            .AsNoTracking()
            .Where(match => match.Id == matchId)
            .Select(match => (bool?)match.Team!.AttendanceEnabled)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<string?> SetEnabledAsync(
        Guid teamId,
        Guid truppId,
        bool enabled,
        CancellationToken cancellationToken)
    {
        var team = await context.Teams
            .FirstOrDefaultAsync(t => t.Id == teamId && t.AgeGroupId == truppId, cancellationToken)
            .ConfigureAwait(false);

        if (team is null)
        {
            return null;
        }

        team.AttendanceEnabled = enabled;

        return team.Slug;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}

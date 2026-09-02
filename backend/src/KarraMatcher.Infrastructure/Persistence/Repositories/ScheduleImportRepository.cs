using System.Globalization;

using KarraMatcher.Application.Abstractions.Persistence;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

internal sealed class ScheduleImportRepository(KarraMatcherDbContext context)
    : IScheduleImportRepository
{
    public async Task<ImportWorld> LoadAsync(string teamSlug, CancellationToken cancellationToken)
    {
        var teams = await context.Teams
            .AsNoTracking()
            .Select(t => new
            {
                t.Slug,
                t.Name,
                AgeGroup = t.AgeGroup!.Name,
                Club = t.AgeGroup.Club!.Name,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var teamsByName = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var team in teams)
        {
            // Flera former for samma lag. Sista skrivningen vinner, men formerna kan inte
            // krocka mellan lag: klubb och aldersgrupp ar samma for alla fyra.
            foreach (var form in new[]
            {
                $"{team.Club} {team.AgeGroup} {team.Name}",
                $"{team.AgeGroup} {team.Name}",
                team.Name,
            })
            {
                teamsByName[form.Trim().ToLowerInvariant()] = team.Slug;
            }
        }

        var venues = await context.Venues
            .AsNoTracking()
            .Select(v => new { v.Id, v.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var existing = await context.Matches
            .AsNoTracking()
            .Where(m => m.Team!.Slug == teamSlug)
            .Select(m => new { m.KickoffUtc, m.OpponentName })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ImportWorld(
            teamsByName,
            venues.ToDictionary(v => v.Name.ToLowerInvariant(), v => v.Id, StringComparer.Ordinal),
            existing
                .Select(m => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{teamSlug}|{m.KickoffUtc:yyyy-MM-ddTHH:mm:ss}Z|{m.OpponentName.ToLowerInvariant()}"))
                .ToHashSet(StringComparer.OrdinalIgnoreCase));
    }
}

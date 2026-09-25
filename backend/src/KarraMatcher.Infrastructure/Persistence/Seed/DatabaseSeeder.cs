using System.Globalization;

using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Common;
using KarraMatcher.Domain.Events;
using KarraMatcher.Domain.Teams;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace KarraMatcher.Infrastructure.Persistence.Seed;

/// <summary>
/// Lägger in startdata. Idempotent: körs den två gånger blir resultatet detsamma
/// som efter en körning, eftersom varje rad slås upp på sin naturliga nyckel.
/// Det är ett krav — seeden körs vid varje driftsättning.
/// </summary>
public sealed partial class DatabaseSeeder(KarraMatcherDbContext context, IConfiguration configuration)
{
    /// <summary>Konfignyckeln för superadminens mejladress. Sätts som miljövariabel i drift.</summary>
    public const string SuperAdminEmailKey = "SuperAdmin:Email";

    public async Task<SeedResult> SeedAsync(CancellationToken cancellationToken = default)
    {
        var sport = await EnsureSportAsync(cancellationToken).ConfigureAwait(false);
        var club = await EnsureClubAsync(cancellationToken).ConfigureAwait(false);
        var ageGroup = await EnsureAgeGroupAsync(club, sport, cancellationToken).ConfigureAwait(false);
        var teams = await EnsureTeamsAsync(ageGroup, cancellationToken).ConfigureAwait(false);
        var venues = await EnsureVenuesAsync(cancellationToken).ConfigureAwait(false);
        var matchesAdded = await EnsureMatchesAsync(teams, venues, cancellationToken)
            .ConfigureAwait(false);

        await EnsureSuperAdminAsync(cancellationToken).ConfigureAwait(false);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Demodata (admin/förälder/barn) för testning — bara när DemoSeed uttryckligen ber om det.
        await EnsureDemoAsync(ageGroup, teams, cancellationToken).ConfigureAwait(false);

        return new SeedResult(teams.Count, venues.Count, matchesAdded);
    }

    private async Task<Sport> EnsureSportAsync(CancellationToken cancellationToken)
    {
        var sport = await context.Sports
            .FirstOrDefaultAsync(s => s.Slug == SeedData.SportSlug, cancellationToken)
            .ConfigureAwait(false);

        if (sport is not null)
        {
            return sport;
        }

        sport = new Sport { Name = SeedData.SportName, Slug = SeedData.SportSlug };
        context.Sports.Add(sport);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return sport;
    }

    /// <summary>
    /// Superadmin — bara ägaren, och bara om <c>SuperAdmin:Email</c> är satt. Aldrig hårdkodad
    /// i repot. Idempotent: kontot slås upp på adressen, rollen på (konto, SuperAdmin).
    /// </summary>
    private async Task EnsureSuperAdminAsync(CancellationToken cancellationToken)
    {
        var email = configuration[SuperAdminEmailKey]?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(email))
        {
            return;
        }

        var account = await context.Accounts
            .FirstOrDefaultAsync(a => a.Email == email, cancellationToken)
            .ConfigureAwait(false);

        if (account is null)
        {
            account = new Account { Email = email, CreatedUtc = DateTime.UtcNow };
            context.Accounts.Add(account);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var hasRole = await context.TeamRoles
            .AnyAsync(
                r => r.AccountId == account.Id && r.Role == RoleKind.SuperAdmin,
                cancellationToken)
            .ConfigureAwait(false);

        if (!hasRole)
        {
            context.TeamRoles.Add(new TeamRole
            {
                AccountId = account.Id,
                Role = RoleKind.SuperAdmin,
                GrantedUtc = DateTime.UtcNow,
            });
        }
    }

    private async Task<Club> EnsureClubAsync(CancellationToken cancellationToken)
    {
        var club = await context.Clubs
            .FirstOrDefaultAsync(c => c.Slug == SeedData.ClubSlug, cancellationToken)
            .ConfigureAwait(false);

        if (club is not null)
        {
            return club;
        }

        club = new Club { Name = SeedData.ClubName, Slug = SeedData.ClubSlug };
        context.Clubs.Add(club);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return club;
    }

    private async Task<AgeGroup> EnsureAgeGroupAsync(
        Club club, Sport sport, CancellationToken cancellationToken)
    {
        var ageGroup = await context.AgeGroups
            .FirstOrDefaultAsync(
                a => a.ClubId == club.Id
                    && a.Name == SeedData.AgeGroupName
                    && a.Season == SeedData.Season,
                cancellationToken)
            .ConfigureAwait(false);

        if (ageGroup is not null)
        {
            // Backa in sporten på en trupp som fanns före v2 (SportId hann bli tomt).
            if (ageGroup.SportId == Guid.Empty)
            {
                ageGroup.SportId = sport.Id;
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            return ageGroup;
        }

        ageGroup = new AgeGroup
        {
            ClubId = club.Id,
            SportId = sport.Id,
            Name = SeedData.AgeGroupName,
            Season = SeedData.Season,
        };

        context.AgeGroups.Add(ageGroup);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ageGroup;
    }

    private async Task<Dictionary<string, Team>> EnsureTeamsAsync(
        AgeGroup ageGroup, CancellationToken cancellationToken)
    {
        var existing = await context.Teams
            .ToDictionaryAsync(t => t.Slug, cancellationToken)
            .ConfigureAwait(false);

        foreach (var row in SeedData.Teams.Where(r => !existing.ContainsKey(r.Slug)))
        {
            var team = new Team
            {
                AgeGroupId = ageGroup.Id,
                Name = row.Name,
                Slug = row.Slug,
                ColorHex = row.ColorHex,
                // Kallelsen levereras avstängd (§KM.7).
                AttendanceEnabled = false,
            };

            context.Teams.Add(team);
            existing[row.Slug] = team;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return existing;
    }

    private async Task<Dictionary<string, Venue>> EnsureVenuesAsync(
        CancellationToken cancellationToken)
    {
        var existing = await context.Venues
            .ToDictionaryAsync(v => v.Name, cancellationToken)
            .ConfigureAwait(false);

        foreach (var row in SeedData.Venues.Where(r => !existing.ContainsKey(r.Name)))
        {
            var venue = new Venue
            {
                Name = row.Name,
                Address = row.Address,
                Latitude = row.Latitude,
                Longitude = row.Longitude,
                IsHome = row.IsHome,
            };

            context.Venues.Add(venue);
            existing[row.Name] = venue;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return existing;
    }

    private async Task<int> EnsureMatchesAsync(
        Dictionary<string, Team> teams,
        Dictionary<string, Venue> venues,
        CancellationToken cancellationToken)
    {
        var existing = await context.Events
            .Where(e => e.Type == EventType.Match)
            .Select(m => new { m.TeamId, m.KickoffUtc, m.OpponentName })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var known = existing
            .Select(m => (m.TeamId, m.KickoffUtc, m.OpponentName))
            .ToHashSet();

        var added = 0;

        foreach (var row in SeedData.Matches)
        {
            var team = teams[row.TeamSlug];
            var venue = venues[row.VenueName];

            var kickoffUtc = SwedishTime.ToUtc(
                DateOnly.Parse(row.Date, CultureInfo.InvariantCulture),
                TimeOnly.Parse(row.Time, CultureInfo.InvariantCulture));

            if (!known.Add((team.Id, kickoffUtc, row.Opponent)))
            {
                continue;
            }

            context.Events.Add(new Event
            {
                TeamId = team.Id,
                Type = EventType.Match,
                KickoffUtc = kickoffUtc,
                OpponentName = row.Opponent,
                VenueId = venue.Id,
                IsHome = venue.IsHome,
                Status = EventStatus.Scheduled,
                UpdatedUtc = DateTime.UtcNow,
            });

            added++;
        }

        return added;
    }
}

/// <summary>Vad seeden faktiskt gjorde. Loggas vid uppstart.</summary>
public sealed record SeedResult(int Teams, int Venues, int MatchesAdded);

using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Matches;
using KarraMatcher.Domain.Teams;

namespace KarraMatcher.Application.Tests;

/// <summary>
/// Handskriven attrapp i stället för ett mockbibliotek. Interfacet har tre metoder, och
/// en attrapp som går att läsa rakt av säger mer om vad testet förväntar sig än en kedja
/// av setup-anrop.
/// </summary>
internal sealed class FakeTeamRepository : ITeamRepository
{
    public List<Team> Teams { get; } = [];

    public List<Match> Matches { get; } = [];

    /// <summary>Vilket lag-id som senast efterfrågades — så att tester kan kontrollera det.</summary>
    public Guid? LastRequestedTeamId { get; private set; }

    public Task<IReadOnlyList<Team>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Team>>([.. Teams.OrderBy(t => t.Name, StringComparer.Ordinal)]);

    public Task<Team?> FindBySlugAsync(string slug, CancellationToken cancellationToken) =>
        Task.FromResult(Teams.FirstOrDefault(t => string.Equals(t.Slug, slug, StringComparison.Ordinal)));

    public Task<IReadOnlyList<Match>> GetMatchesAsync(Guid teamId, CancellationToken cancellationToken)
    {
        LastRequestedTeamId = teamId;

        return Task.FromResult<IReadOnlyList<Match>>(
            [.. Matches.Where(m => m.TeamId == teamId).OrderBy(m => m.KickoffUtc)]);
    }

    // ---- Byggare för testdata ---------------------------------------------------------

    public static AgeGroup NewAgeGroup(string name = "P2016") => new()
    {
        Id = Guid.NewGuid(),
        ClubId = Guid.NewGuid(),
        Name = name,
        Season = "2026",
    };

    public Team AddTeam(string slug, string name, string colorHex = "#D9A21B", AgeGroup? ageGroup = null)
    {
        var team = new Team
        {
            Id = Guid.NewGuid(),
            AgeGroupId = Guid.NewGuid(),
            AgeGroup = ageGroup ?? NewAgeGroup(),
            Name = name,
            ColorHex = colorHex,
            Slug = slug,
        };

        Teams.Add(team);
        return team;
    }

    public Match AddMatch(
        Team team,
        DateTime kickoffUtc,
        string opponent = "Motstandarna",
        MatchStatus status = MatchStatus.Scheduled,
        string? addressOverride = null,
        Venue? venue = null)
    {
        var match = new Match
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            KickoffUtc = kickoffUtc,
            OpponentName = opponent,
            VenueId = Guid.NewGuid(),
            Venue = venue ?? NewVenue(),
            AddressOverride = addressOverride,
            IsHome = true,
            Status = status,
        };

        Matches.Add(match);
        return match;
    }

    public static Venue NewVenue(
        string name = "Karra IP",
        string address = "Idrottsvagen 1, Goteborg",
        double latitude = 57.79,
        double longitude = 11.94) => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Address = address,
            Latitude = latitude,
            Longitude = longitude,
            IsHome = true,
        };
}

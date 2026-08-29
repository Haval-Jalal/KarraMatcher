using System.Net;
using System.Text.Json;

using KarraMatcher.Domain.Matches;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// De publika endpointsen för lag och matcher — appens mest anropade yta.
///
/// <para>
/// Testerna körs mot hela pipelinen, eftersom det är där kraven faktiskt bor: anonym
/// åtkomst (§KM.3), inga personuppgifter i svaret, och cache-headers som låter Vercels
/// edge svara utan att väcka Render (§KM.11).
/// </para>
/// </summary>
public sealed class TeamEndpointTests : IClassFixture<KarraMatcherApiFactory>
{
    private static readonly DateTime Kickoff = new(2026, 9, 5, 12, 30, 0, DateTimeKind.Utc);
    private readonly KarraMatcherApiFactory _factory;

    public TeamEndpointTests(KarraMatcherApiFactory factory)
    {
        _factory = factory;
        Seed(factory);
    }

    /// <summary>
    /// Lägger in ett känt schema i minnesdatabasen. Idempotent, eftersom xUnit skapar en
    /// ny testklassinstans per test medan fixturen — och därmed databasen — delas.
    /// </summary>
    private static void Seed(KarraMatcherApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        if (context.Teams.Any())
        {
            return;
        }

        var club = new Club { Id = Guid.NewGuid(), Name = "Karra KIF", Slug = "karra-kif" };
        var ageGroup = new AgeGroup
        {
            Id = Guid.NewGuid(),
            ClubId = club.Id,
            Name = "P2016",
            Season = "2026",
        };
        var gul = new Team
        {
            Id = Guid.NewGuid(),
            AgeGroupId = ageGroup.Id,
            Name = "Gul",
            ColorHex = "#D9A21B",
            Slug = "gul",
        };
        var bla = new Team
        {
            Id = Guid.NewGuid(),
            AgeGroupId = ageGroup.Id,
            Name = "Bla",
            ColorHex = "#1B5FD9",
            Slug = "bla",
        };
        var venue = new Venue
        {
            Id = Guid.NewGuid(),
            Name = "Karra IP",
            Address = "Idrottsvagen 1, Goteborg",
            Latitude = 57.79,
            Longitude = 11.94,
            IsHome = true,
        };

        context.Clubs.Add(club);
        context.AgeGroups.Add(ageGroup);
        context.Teams.AddRange(gul, bla);
        context.Venues.Add(venue);

        // Med flit i fel ordning: svaret ska komma sorterat oavsett insättningsordning.
        context.Matches.AddRange(
            NewMatch(gul, venue, Kickoff.AddDays(14), "Sist", MatchStatus.Scheduled),
            NewMatch(gul, venue, Kickoff, "Forst", MatchStatus.Scheduled),
            NewMatch(gul, venue, Kickoff.AddDays(7), "Installd", MatchStatus.Cancelled));

        context.SaveChanges();
    }

    private static Match NewMatch(
        Team team, Venue venue, DateTime kickoffUtc, string opponent, MatchStatus status) => new()
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            KickoffUtc = kickoffUtc,
            OpponentName = opponent,
            VenueId = venue.Id,
            IsHome = true,
            Status = status,
            UpdatedUtc = DateTime.UtcNow,
        };

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    // ---- Lagen -----------------------------------------------------------------------

    [Fact]
    public async Task GetTeams_UtanInloggning_Svarar200()
    {
        // §KM.0 A4: en förälder som bara vill se matchtiden ska aldrig mötas av inloggning.
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/teams", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetTeams_GerLagenMedFargOchAldersgrupp()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/teams", CancellationToken.None);
        var json = await ReadJsonAsync(response);

        Assert.Equal(2, json.GetArrayLength());
        var first = json[0];
        Assert.False(string.IsNullOrWhiteSpace(first.GetProperty("slug").GetString()));
        Assert.StartsWith("#", first.GetProperty("colorHex").GetString(), StringComparison.Ordinal);
        Assert.Equal("P2016", first.GetProperty("ageGroup").GetString());
    }

    [Fact]
    public async Task GetTeams_HarEdgeCacheOchEtag()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/teams", CancellationToken.None);
        var cacheControl = response.Headers.CacheControl?.ToString();

        Assert.NotNull(cacheControl);
        Assert.Contains("s-maxage=3600", cacheControl, StringComparison.Ordinal);
        Assert.NotNull(response.Headers.ETag);
    }

    // ---- Matcherna -------------------------------------------------------------------

    [Fact]
    public async Task GetTeamMatches_GerMatcherSorteradePaAvspark()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/teams/gul/matches", CancellationToken.None);
        var json = await ReadJsonAsync(response);
        var matches = json.GetProperty("matches");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            ["Forst", "Installd", "Sist"],
            matches.EnumerateArray().Select(m => m.GetProperty("opponent").GetString()));
    }

    [Fact]
    public async Task GetTeamMatches_InstalldMatchArMarkt()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/teams/gul/matches", CancellationToken.None);
        var json = await ReadJsonAsync(response);

        var cancelled = json.GetProperty("matches").EnumerateArray()
            .Single(m => m.GetProperty("opponent").GetString() == "Installd");

        Assert.Equal("Cancelled", cancelled.GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetTeamMatches_AvsparkArUtc()
    {
        // §KM.5. Skulle backend börja skicka lokaltid vore felet osynligt halva året.
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/teams/gul/matches", CancellationToken.None);
        var json = await ReadJsonAsync(response);

        var kickoff = json.GetProperty("matches")[0].GetProperty("kickoffUtc").GetString();

        Assert.NotNull(kickoff);
        Assert.EndsWith("+00:00", kickoff, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTeamMatches_HarEdgeCacheForSchema()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/teams/gul/matches", CancellationToken.None);

        Assert.Contains(
            "s-maxage=300",
            response.Headers.CacheControl?.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTeamMatches_OforandratSchema_Ger304()
    {
        using var client = _factory.CreateClient();

        var first = await client.GetAsync("/api/v1/teams/gul/matches", CancellationToken.None);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/teams/gul/matches");
        request.Headers.TryAddWithoutValidation("If-None-Match", first.Headers.ETag!.ToString());
        var second = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
    }

    [Fact]
    public async Task GetTeamMatches_OkantLag_Ger404MedProblemDetails()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/teams/finns-inte/matches", CancellationToken.None);
        var json = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Laget finns inte", json.GetProperty("title").GetString());
    }

    [Theory]
    [InlineData("Gul")]
    [InlineData("gul%20lag")]
    [InlineData("gul'%20or%201=1--")]
    public async Task GetTeamMatches_OgiltigSlug_Ger400(string slug)
    {
        // Skräp avvisas av validatorn innan det når databasen, och blir 400 — inte 500.
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/teams/{slug}/matches", CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTeamMatches_SvaretInnehallerIngaPersonuppgifter()
    {
        // §KM.3 och säkerhetschecklistan 4.7: publika endpoints returnerar aldrig PII.
        // Testet låser fältuppsättningen — dyker ett nytt fält upp måste någon ta
        // ställning till det här, inte upptäcka det i produktion.
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/teams/gul/matches", CancellationToken.None);
        var json = await ReadJsonAsync(response);

        var matchFields = json.GetProperty("matches")[0]
            .EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal);

        Assert.Equal(
            ["address", "id", "isHome", "kickoffUtc", "opponent", "status", "venue"],
            matchFields);
    }
}

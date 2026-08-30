using System.Net;
using System.Text.Json;

using KarraMatcher.Domain.Matches;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Den publika endpointen för en enskild match. Matchdetaljsidan behöver mer än listan
/// visar: adressen till kartlänken och koordinaterna till väderprognosen.
/// </summary>
public sealed class MatchEndpointTests : IClassFixture<KarraMatcherApiFactory>
{
    private static readonly DateTime Kickoff = new(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
    private readonly KarraMatcherApiFactory _factory;
    private readonly Guid _matchId;

    public MatchEndpointTests(KarraMatcherApiFactory factory)
    {
        _factory = factory;
        _matchId = Seed(factory);
    }

    /// <summary>
    /// Lägger in en känd match. Idempotent, eftersom xUnit skapar en ny testklassinstans
    /// per test medan fixturen — och därmed databasen — delas.
    /// </summary>
    private static Guid Seed(KarraMatcherApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var existing = context.Matches.FirstOrDefault(m => m.OpponentName == "Detaljmotstandaren");

        if (existing is not null)
        {
            return existing.Id;
        }

        var club = new Club { Id = Guid.NewGuid(), Name = "Karra KIF", Slug = "karra-kif-detalj" };
        var ageGroup = new AgeGroup
        {
            Id = Guid.NewGuid(),
            ClubId = club.Id,
            Name = "P2016",
            Season = "2026",
        };
        var team = new Team
        {
            Id = Guid.NewGuid(),
            AgeGroupId = ageGroup.Id,
            Name = "Detaljlaget",
            ColorHex = "#D9A21B",
            Slug = "detaljlaget",
        };
        var venue = new Venue
        {
            Id = Guid.NewGuid(),
            Name = "Klarebergsvallen",
            Address = "Klarebergsvallen, Karra",
            Latitude = 57.78,
            Longitude = 11.99,
            IsHome = true,
        };
        var match = new Match
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            KickoffUtc = Kickoff,
            OpponentName = "Detaljmotstandaren",
            VenueId = venue.Id,
            IsHome = false,
            Status = MatchStatus.Scheduled,

            // Skrivs med flit: testet längst ned kontrollerar att den inte kommer med i
            // svaret. En notis är tränarens fritext och räknas som potentiell PII (§KM.1).
            Note = "Ta med gula tröjor. Kalle spelar inte, han är sjuk.",
            UpdatedUtc = DateTime.UtcNow,
        };

        context.Clubs.Add(club);
        context.AgeGroups.Add(ageGroup);
        context.Teams.Add(team);
        context.Venues.Add(venue);
        context.Matches.Add(match);
        context.SaveChanges();

        return match.Id;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    [Fact]
    public async Task GetMatch_UtanInloggning_Svarar200()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/matches/{_matchId}", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetMatch_GerMatchenOchLaget()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/matches/{_matchId}", CancellationToken.None);
        var json = await ReadJsonAsync(response);

        Assert.Equal("Detaljmotstandaren", json.GetProperty("match").GetProperty("opponent").GetString());
        Assert.False(json.GetProperty("match").GetProperty("isHome").GetBoolean());
        Assert.Equal("detaljlaget", json.GetProperty("team").GetProperty("slug").GetString());
        Assert.Equal("P2016", json.GetProperty("team").GetProperty("ageGroup").GetString());
    }

    [Fact]
    public async Task GetMatch_GerKoordinaterFranSpelplatsen()
    {
        // Koordinaterna driver väderprognosen och kommer ur vår egen Venue-tabell —
        // aldrig från något anroparen skickat in (SSRF-regeln i CLAUDE.md).
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/matches/{_matchId}", CancellationToken.None);
        var venue = (await ReadJsonAsync(response)).GetProperty("match").GetProperty("venue");

        Assert.Equal(57.78, venue.GetProperty("latitude").GetDouble(), 2);
        Assert.Equal(11.99, venue.GetProperty("longitude").GetDouble(), 2);
    }

    [Fact]
    public async Task GetMatch_AvsparkArUtc()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/matches/{_matchId}", CancellationToken.None);
        var kickoff = (await ReadJsonAsync(response))
            .GetProperty("match").GetProperty("kickoffUtc").GetString();

        Assert.EndsWith("+00:00", kickoff, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetMatch_HarEdgeCache()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/matches/{_matchId}", CancellationToken.None);

        Assert.Contains(
            "s-maxage=300",
            response.Headers.CacheControl?.ToString(),
            StringComparison.Ordinal);
        Assert.NotNull(response.Headers.ETag);
    }

    [Fact]
    public async Task GetMatch_OkantId_Ger404MedProblemDetails()
    {
        // En gammal kalenderpost från förra säsongen ska ge ett begripligt svar.
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/v1/matches/{Guid.NewGuid()}", CancellationToken.None);
        var json = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Matchen finns inte", json.GetProperty("title").GetString());
    }

    [Theory]
    [InlineData("inte-en-guid")]
    [InlineData("12345")]
    public async Task GetMatch_IdSomInteArEnGuid_Ger404UtanAttNaDatabasen(string id)
    {
        // Routningsvillkoret {id:guid} avvisar skräp innan något körs. Det blir 404 och
        // inte 500 — en felformad länk är inte ett serverfel.
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/matches/{id}", CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetMatch_SvaretInnehallerIngenNotisOchIngenPii()
    {
        // §KM.1 och §KM.3. Matchen i testdatan har en notis som innehåller ett barns namn
        // och en hälsouppgift — precis det en tränare kan råka skriva. Den får inte finnas
        // i ett publikt, edge-cachat svar.
        //
        // Fältuppsättningen låses samtidigt: dyker ett nytt fält upp måste någon ta
        // ställning till det här, inte upptäcka det i produktion.
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/matches/{_matchId}", CancellationToken.None);
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);
        var json = JsonDocument.Parse(body).RootElement;

        Assert.DoesNotContain("Kalle", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sjuk", body, StringComparison.OrdinalIgnoreCase);

        var matchFields = json.GetProperty("match")
            .EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal);

        Assert.Equal(
            ["address", "id", "isHome", "kickoffUtc", "opponent", "status", "venue"],
            matchFields);
    }
}

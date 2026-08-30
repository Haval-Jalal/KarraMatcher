using System.Net;

using KarraMatcher.Domain.Matches;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Kalenderfeeden per lag (§KM.4).
///
/// <para>
/// Adressen ligger utanför <c>/api</c> med flit: den klistras in i en kalenderapp av en
/// människa. Testerna kör därför mot den riktiga routen, eftersom just den sökvägen är en
/// del av kontraktet — och den som Vercel-rewriten måste täcka.
/// </para>
/// </summary>
public sealed class CalendarFeedTests : IClassFixture<KarraMatcherApiFactory>
{
    private static readonly DateTime Kickoff = new(2026, 8, 30, 11, 15, 0, DateTimeKind.Utc);
    private readonly KarraMatcherApiFactory _factory;

    public CalendarFeedTests(KarraMatcherApiFactory factory)
    {
        _factory = factory;
        Seed(factory);
    }

    private static void Seed(KarraMatcherApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        if (context.Teams.Any(t => t.Slug == "kalenderlaget"))
        {
            return;
        }

        var club = new Club { Id = Guid.NewGuid(), Name = "Karra KIF", Slug = "karra-kif-kalender" };
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
            Name = "Blå",
            ColorHex = "#1E3F8A",
            Slug = "kalenderlaget",
        };
        var venue = new Venue
        {
            Id = Guid.NewGuid(),
            Name = "Klarebergsvallen",
            Address = "Klarebergsvallen, Kärra, Göteborg",
            Latitude = 57.78,
            Longitude = 11.99,
            IsHome = true,
        };

        context.Clubs.Add(club);
        context.AgeGroups.Add(ageGroup);
        context.Teams.Add(team);
        context.Venues.Add(venue);
        context.Matches.AddRange(
            NewMatch(team, venue, Kickoff, "Torslanda IK", MatchStatus.Scheduled, 0),
            NewMatch(team, venue, Kickoff.AddDays(7), "Öckerö IF", MatchStatus.Cancelled, 2));

        context.SaveChanges();
    }

    private static Match NewMatch(
        Team team, Venue venue, DateTime kickoffUtc, string opponent, MatchStatus status, int sequence) => new()
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            KickoffUtc = kickoffUtc,
            OpponentName = opponent,
            VenueId = venue.Id,
            IsHome = true,
            Status = status,
            IcsSequence = sequence,
            UpdatedUtc = DateTime.UtcNow,
        };

    private async Task<string> FeedAsync(string slug = "kalenderlaget")
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/calendar/{slug}.ics", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Feed_UtanInloggning_Svarar200()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/calendar/kalenderlaget.ics", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Feed_HarRattInnehallstypOchTeckenkodning()
    {
        // Utan charset gissar en del kalenderappar Latin-1 och visar "BlÃ¥" i varje rubrik.
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/calendar/kalenderlaget.ics", CancellationToken.None);

        Assert.Equal("text/calendar", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType?.CharSet);
    }

    [Fact]
    public async Task Feed_SvenskaTeckenOverlever()
    {
        var feed = await FeedAsync();

        Assert.Contains("Öckerö IF", feed, StringComparison.Ordinal);
        Assert.Contains("Kärra", feed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Feed_InstalldMatchArCancelled()
    {
        // §KM.4: annars ligger den kvar i föräldrarnas kalendrar som om den spelades.
        var feed = await FeedAsync();

        Assert.Contains("STATUS:CANCELLED", feed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Feed_SequenceFoljerMed()
    {
        var feed = await FeedAsync();

        Assert.Contains("SEQUENCE:2", feed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Feed_AllaRaderAvslutasMedCrLf()
    {
        // LF ensamt avvisas av vissa klienter, och felet syns bara som en kalender som
        // aldrig fylls med något.
        var feed = await FeedAsync();

        Assert.DoesNotContain(feed.Replace("\r\n", string.Empty, StringComparison.Ordinal), "\n", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Feed_ArCachadPaEdge()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/calendar/kalenderlaget.ics", CancellationToken.None);

        Assert.Contains(
            "s-maxage=900",
            response.Headers.CacheControl?.ToString(),
            StringComparison.Ordinal);
        Assert.NotNull(response.Headers.ETag);
    }

    [Fact]
    public async Task Feed_OforandratSchema_Ger304()
    {
        // En kalenderapp hämtar om var sjätte timme i åratal. Ett villkorat svar sparar
        // både Renders instanstimmar och föräldrarnas mobildata.
        using var client = _factory.CreateClient();

        var first = await client.GetAsync("/calendar/kalenderlaget.ics", CancellationToken.None);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/calendar/kalenderlaget.ics");
        request.Headers.TryAddWithoutValidation("If-None-Match", first.Headers.ETag!.ToString());
        var second = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
    }

    [Fact]
    public async Task Feed_OkantLag_Ger404()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/calendar/finns-inte.ics", CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("Gul")]
    [InlineData("gul%20lag")]
    public async Task Feed_OgiltigSlug_Ger400(string slug)
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/calendar/{slug}.ics", CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using KarraMatcher.Application.Abstractions.Security;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Carpool;
using KarraMatcher.Domain.Events;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Tränarens samåkningsöverblick (`#55`, §KM.12).
///
/// <para>
/// Frågan överblicken finns för är "får alla skjuts till bortamatchen?". Testerna vaktar
/// därför två saker: att en match <em>utan</em> förare kommer med — det är den raden som
/// betyder något — och att överblicken hör till laget och inte till vem som helst med ett
/// konto.
/// </para>
/// </summary>
public sealed class CarpoolOverviewTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private const string Note = "Kor forbi Skogome, hor av er";

    private sealed record Fixture(string Slug, Guid CoachId, Guid DriverId, Guid MatchWithOffer);

    /// <summary>Ett lag med fyra matcher: en med förare, en utan, en spelad och en inställd.</summary>
    private async Task<Fixture> SeedAsync(string suffix)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var now = DateTime.UtcNow;

        var club = new Club { Id = Guid.NewGuid(), Name = "Karra KIF", Slug = $"klubb-o-{suffix}" };
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
            Name = "Gul",
            ColorHex = "#D9A21B",
            Slug = $"gul-o-{suffix}",
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

        var withOffer = Match(team.Id, venue.Id, now.AddDays(3), "Torslanda");
        var withoutOffer = Match(team.Id, venue.Id, now.AddDays(10), "Backa");
        var played = Match(team.Id, venue.Id, now.AddDays(-3), "Hisingsbacka");
        var cancelled = Match(team.Id, venue.Id, now.AddDays(5), "Salvia");
        cancelled.Status = EventStatus.Cancelled;

        var coach = new Account { Id = Guid.NewGuid(), Email = $"tranare-o-{suffix}@example.com" };
        var driver = new Account { Id = Guid.NewGuid(), Email = $"forare-o-{suffix}@example.com" };
        var asker = new Account { Id = Guid.NewGuid(), Email = $"fragare-o-{suffix}@example.com" };

        var offer = new CarpoolOffer
        {
            Id = Guid.NewGuid(),
            MatchId = withOffer.Id,
            DriverAccountId = driver.Id,
            Direction = CarpoolDirection.ToMatch,
            DeparturePlace = "Karra centrum",
            DepartureUtc = withOffer.KickoffUtc.AddHours(-1),
            Seats = 3,
            Note = Note,
            Status = CarpoolOfferStatus.Open,
            CreatedUtc = now,
            UpdatedUtc = now,
        };

        // En accepterad tar en plats, en vantande tar ingen (§KM.12).
        var accepted = Request(offer.Id, asker.Id, 1, CarpoolRequestStatus.Accepted, now);
        var waiting = Request(offer.Id, coach.Id, 2, CarpoolRequestStatus.Pending, now);

        context.Clubs.Add(club);
        context.AgeGroups.Add(ageGroup);
        context.Teams.Add(team);
        context.Venues.Add(venue);
        context.Events.AddRange(withOffer, withoutOffer, played, cancelled);
        context.Accounts.AddRange(coach, driver, asker);
        context.CarpoolOffers.Add(offer);
        context.CarpoolRequests.AddRange(accepted, waiting);

        await context.SaveChangesAsync(CancellationToken.None);

        return new Fixture(team.Slug, coach.Id, driver.Id, withOffer.Id);
    }

    private static Event Match(Guid teamId, Guid venueId, DateTime kickoffUtc, string opponent) =>
        new()
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            KickoffUtc = kickoffUtc,
            OpponentName = opponent,
            VenueId = venueId,
            IsHome = false,
            Status = EventStatus.Scheduled,
            IcsSequence = 0,
            UpdatedUtc = kickoffUtc,
        };

    private static CarpoolRequest Request(
        Guid offerId,
        Guid accountId,
        int seats,
        CarpoolRequestStatus status,
        DateTime now) =>
        new()
        {
            Id = Guid.NewGuid(),
            OfferId = offerId,
            RequesterAccountId = accountId,
            Seats = seats,
            Status = status,
            CreatedUtc = now,
            UpdatedUtc = now,
        };

    private string TokenFor(Guid accountId, params string[] coachOf)
    {
        using var scope = factory.Services.CreateScope();
        var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();

        return issuer.Issue(accountId, "tranare@example.com", new AccountRoles(false, [], coachOf)).Token;
    }

    private async Task<HttpResponseMessage> GetAsync(string slug, string? token)
    {
        using var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/teams/{slug}/carpool");

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request, CancellationToken.None);
    }

    // ---- Vem som far se ---------------------------------------------------------------

    [Fact]
    public async Task Overblick_UtanInloggning_Nekas()
    {
        // Schemat ar allas, men vem som kor vem ar lagets egen sak (§KM.3, §KM.12).
        var fixture = await SeedAsync("anon");

        var response = await GetAsync(fixture.Slug, token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Overblick_SomForalderUtanTranarskap_Nekas()
    {
        // Ett konto racker inte. Foraldern ser sin egen samakning pa matchsidan.
        var fixture = await SeedAsync("foralder");

        var response = await GetAsync(fixture.Slug, TokenFor(fixture.DriverId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Overblick_SomTranareForAnnatLag_Nekas()
    {
        // Laget star i adressen, sa det finns inget lagfalt att skicka i stallet.
        var fixture = await SeedAsync("annat-lag");

        var response = await GetAsync(fixture.Slug, TokenFor(fixture.CoachId, "nagot-annat-lag"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- Vad tranaren ser -------------------------------------------------------------

    [Fact]
    public async Task Overblick_ListarMatcherUtanForare()
    {
        /*
         * Karnan i #55. En overblick som bara visade det som redan ar ordnat hade varit
         * trevlig att titta pa och omojlig att agera pa -- det ar matchen dar ingen erbjudit
         * skjuts som tranaren ska hora av sig om.
         */
        var fixture = await SeedAsync("utan-forare");

        var response = await GetAsync(fixture.Slug, TokenFor(fixture.CoachId, fixture.Slug));

        response.EnsureSuccessStatusCode();

        var rows = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        var backa = rows.EnumerateArray()
            .Single(row => row.GetProperty("opponent").GetString() == "Backa");

        Assert.True(backa.GetProperty("needsDriver").GetBoolean());
        Assert.Equal(0, backa.GetProperty("seatsOffered").GetInt32());
    }

    [Fact]
    public async Task Overblick_RaknarPlatserEfterAccepterade()
    {
        // Bara accepterade forfragningar forbrukar platser (§KM.12).
        var fixture = await SeedAsync("platser");

        var response = await GetAsync(fixture.Slug, TokenFor(fixture.CoachId, fixture.Slug));

        response.EnsureSuccessStatusCode();

        var rows = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        var torslanda = rows.EnumerateArray()
            .Single(row => row.GetProperty("opponent").GetString() == "Torslanda");

        Assert.Equal(3, torslanda.GetProperty("seatsOffered").GetInt32());
        Assert.Equal(1, torslanda.GetProperty("seatsTaken").GetInt32());
        Assert.Equal(2, torslanda.GetProperty("seatsLeft").GetInt32());
        Assert.Equal(1, torslanda.GetProperty("pendingRequests").GetInt32());
        Assert.False(torslanda.GetProperty("needsDriver").GetBoolean());
    }

    [Fact]
    public async Task Overblick_UtelamnarSpeladeOchInstalldaMatcher()
    {
        // Ingen behover skjuts till en match som inte spelas, och en spelad match ar inte
        // langre en fraga -- den gallras dessutom bort efter trettio dagar.
        var fixture = await SeedAsync("urval");

        var response = await GetAsync(fixture.Slug, TokenFor(fixture.CoachId, fixture.Slug));

        response.EnsureSuccessStatusCode();

        var rows = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        var opponents = rows.EnumerateArray()
            .Select(row => row.GetProperty("opponent").GetString())
            .ToArray();

        Assert.Contains("Torslanda", opponents);
        Assert.Contains("Backa", opponents);
        Assert.DoesNotContain("Hisingsbacka", opponents);
        Assert.DoesNotContain("Salvia", opponents);
    }

    [Fact]
    public async Task Overblick_HamtarAldrigHalsningarna()
    {
        /*
         * §KM.12 tillater tranaren att se fritexten, men overblicken behover den inte for
         * att svara pa sin fraga -- och det som inte behovs ska inte hamtas. Forarens notis
         * foljer med, eftersom den ofta sager var bilen gar ifran.
         */
        var fixture = await SeedAsync("fritext");

        var response = await GetAsync(fixture.Slug, TokenFor(fixture.CoachId, fixture.Slug));
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.Contains(Note, body, StringComparison.Ordinal);
        Assert.DoesNotContain("message", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Overblick_LamnarAldrigUtEnMejladress()
    {
        // Servern lagrar inget namn, bara en adress -- och den visas aldrig for nagon annan.
        var fixture = await SeedAsync("adress");

        var response = await GetAsync(fixture.Slug, TokenFor(fixture.CoachId, fixture.Slug));
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.DoesNotContain("@example.com", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Overblick_ForOkantLag_Svarar404()
    {
        var fixture = await SeedAsync("okant");

        var response = await GetAsync("finns-inte", TokenFor(fixture.CoachId, "finns-inte"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

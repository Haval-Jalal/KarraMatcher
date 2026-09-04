using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using KarraMatcher.Application.Abstractions.Security;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Carpool;
using KarraMatcher.Domain.Matches;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Åkförfrågan (§KM.12).
///
/// <para>
/// Föraren väljer vem som åker med — det är inte först till kvarn. Att fråga tar därför
/// ingen plats i anspråk, och en förfrågan går att skicka <b>även när erbjudandet är
/// fullt</b>, så att föraren kan svara "någon annan hann före" i stället för att den som
/// frågar möts av en död knapp.
/// </para>
/// </summary>
public sealed class CarpoolRequestTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private static readonly DateTime Kickoff = new(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);

    private const string Greeting = "Hej! Vi bor vid Sandeslätt, går det bra att vi hakar på?";

    private static WebApplicationFactoryClientOptions ClientOptions => new() { HandleCookies = true };

    private sealed record Fixture(Guid MatchId, Guid OfferId, Guid DriverId, Guid AskerId, Guid ThirdId);

    /// <summary>En match, ett erbjudande med en plats, och tre konton.</summary>
    private async Task<Fixture> SeedAsync(string suffix, int seats = 1)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var club = new Club { Id = Guid.NewGuid(), Name = "Karra KIF", Slug = $"klubb-r-{suffix}" };
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
            Slug = $"gul-r-{suffix}",
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
        var match = new Match
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            KickoffUtc = Kickoff,
            OpponentName = "Torslanda",
            VenueId = venue.Id,
            IsHome = false,
            Status = MatchStatus.Scheduled,
            IcsSequence = 0,
            UpdatedUtc = Kickoff,
        };

        var driver = new Account { Id = Guid.NewGuid(), Email = $"forare-r-{suffix}@example.com" };
        var asker = new Account { Id = Guid.NewGuid(), Email = $"fragare-r-{suffix}@example.com" };
        var third = new Account { Id = Guid.NewGuid(), Email = $"tredje-r-{suffix}@example.com" };

        var offer = new CarpoolOffer
        {
            Id = Guid.NewGuid(),
            MatchId = match.Id,
            DriverAccountId = driver.Id,
            Direction = CarpoolDirection.Both,
            DeparturePlace = "Karra centrum",
            DepartureUtc = Kickoff.AddHours(-1),
            Seats = seats,
            Status = CarpoolOfferStatus.Open,
            CreatedUtc = Kickoff,
            UpdatedUtc = Kickoff,
        };

        context.Clubs.Add(club);
        context.AgeGroups.Add(ageGroup);
        context.Teams.Add(team);
        context.Venues.Add(venue);
        context.Matches.Add(match);
        context.Accounts.AddRange(driver, asker, third);
        context.CarpoolOffers.Add(offer);

        await context.SaveChangesAsync(CancellationToken.None);

        return new Fixture(match.Id, offer.Id, driver.Id, asker.Id, third.Id);
    }

    private static string TokenFor(IServiceProvider services, Guid accountId)
    {
        using var scope = services.CreateScope();
        var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();

        return issuer.Issue(accountId, "foralder@example.com", new AccountRoles(false, [])).Token;
    }

    private static async Task<(string Token, string Cookie)> GetCsrfAsync(
        HttpClient client,
        string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/csrf");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        var cookie = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("karra_csrf", StringComparison.Ordinal))
            .Split(';')[0];

        return (body.GetProperty("token").GetString()!, cookie);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        Guid actorId,
        object? payload = null)
    {
        var token = TokenFor(factory.Services, actorId);

        using var client = factory.CreateClient(ClientOptions);
        var (csrf, cookie) = await GetCsrfAsync(client, token);

        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        request.Headers.Add("Cookie", cookie);

        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        return await client.SendAsync(request, CancellationToken.None);
    }

    private static string RequestsPath(Fixture fixture) =>
        $"/api/v1/matches/{fixture.MatchId}/carpool/offers/{fixture.OfferId}/requests";

    private Task<HttpResponseMessage> AskAsync(
        Fixture fixture,
        Guid actorId,
        int seats = 1,
        string? message = Greeting) =>
        SendAsync(HttpMethod.Post, RequestsPath(fixture), actorId, new { seats, message });

    private async Task<Guid> AskedAsync(Fixture fixture, Guid actorId, int seats = 1)
    {
        var response = await AskAsync(fixture, actorId, seats);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        return body.GetProperty("id").GetGuid();
    }

    // ---- Kraver inloggning -----------------------------------------------------------

    [Fact]
    public async Task Fraga_UtanInloggning_Nekas()
    {
        var fixture = await SeedAsync("anon");

        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            RequestsPath(fixture),
            new { seats = 1, message = Greeting },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Lista_UtanInloggning_Nekas()
    {
        /*
         * Till skillnad fran erbjudandena, som vem som helst far se (§KM.3), ar forfragan
         * nagot mellan tva foraldrar -- och halsningen ar fritext som bara far na de
         * inblandade (§KM.12).
         */
        var fixture = await SeedAsync("anon-lista");

        using var client = factory.CreateClient();

        var response = await client.GetAsync(RequestsPath(fixture), CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Gar att skicka aven nar erbjudandet ar fullt ---------------------------------

    [Fact]
    public async Task Fraga_OmFlerPlatserAnErbjudandetHar_GarAnda()
    {
        /*
         * Erbjudandet har en plats; har fragas det om fyra. Det ska ga -- foraren svarar
         * "nagon annan hann fore" i stallet for att den som fragar mots av ett formularfel.
         * Platsrakningen sker forst vid accept (#52).
         */
        var fixture = await SeedAsync("fullt", seats: 1);

        var response = await AskAsync(fixture, fixture.AskerId, seats: 4);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Fraga_NarNagonAnnanRedanFragat_GarAnda()
    {
        // Flera far ko pa samma plats. Det ar foraren som valjer, inte forst till kvarn.
        var fixture = await SeedAsync("ko", seats: 1);

        await AskedAsync(fixture, fixture.AskerId);

        var second = await AskAsync(fixture, fixture.ThirdId);

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    // ---- En aktiv forfragan per person och erbjudande ---------------------------------

    [Fact]
    public async Task Fraga_TvaGanger_Avvisas()
    {
        var fixture = await SeedAsync("dubbelt");

        await AskedAsync(fixture, fixture.AskerId);

        var second = await AskAsync(fixture, fixture.AskerId);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Fraga_IgenEfterAttHaAtertagit_GarBra()
    {
        /*
         * En atertagen forfragan blockerar inte. Planerna kan ha andrats tillbaka, och att
         * lasa ute nagon for att de fragat en gang vore fel.
         */
        var fixture = await SeedAsync("igen");

        var requestId = await AskedAsync(fixture, fixture.AskerId);

        await SendAsync(
            HttpMethod.Post,
            $"/api/v1/matches/{fixture.MatchId}/carpool/requests/{requestId}/retract",
            fixture.AskerId);

        var again = await AskAsync(fixture, fixture.AskerId);

        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    // ---- Atertagande -----------------------------------------------------------------

    [Fact]
    public async Task Atertag_SomFragare_Fungerar()
    {
        var fixture = await SeedAsync("atertag");
        var requestId = await AskedAsync(fixture, fixture.AskerId);

        var response = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/matches/{fixture.MatchId}/carpool/requests/{requestId}/retract",
            fixture.AskerId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        var stored = await context.CarpoolRequests.AsNoTracking()
            .SingleAsync(r => r.Id == requestId, CancellationToken.None);

        Assert.Equal(CarpoolRequestStatus.Retracted, stored.Status);
    }

    [Fact]
    public async Task Atertag_SomNagonAnnan_Nekas()
    {
        // Objektnivå-auktorisering: aven foraren far inte atertaga nagon annans forfragan.
        var fixture = await SeedAsync("atertag-fel");
        var requestId = await AskedAsync(fixture, fixture.AskerId);

        var response = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/matches/{fixture.MatchId}/carpool/requests/{requestId}/retract",
            fixture.DriverId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        var stored = await context.CarpoolRequests.AsNoTracking()
            .SingleAsync(r => r.Id == requestId, CancellationToken.None);

        Assert.Equal(CarpoolRequestStatus.Pending, stored.Status);
    }

    // ---- Vem som ser vad -------------------------------------------------------------

    [Fact]
    public async Task Foraren_SerAllaForfragningar()
    {
        var fixture = await SeedAsync("forare-ser");

        await AskedAsync(fixture, fixture.AskerId);
        await AskedAsync(fixture, fixture.ThirdId);

        var response = await SendAsync(HttpMethod.Get, RequestsPath(fixture), fixture.DriverId);
        var found = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        Assert.Equal(2, found.GetArrayLength());
        Assert.Equal(Greeting, found[0].GetProperty("message").GetString());
    }

    [Fact]
    public async Task EnAnnanForalder_SerBaraSinEgen()
    {
        /*
         * Halsningen ar fritext och visas bara for de inblandade (§KM.12). Den som fragat
         * ska inte kunna lasa vad grannen skrev till samma forare.
         */
        var fixture = await SeedAsync("bara-min");

        await AskedAsync(fixture, fixture.AskerId);
        await AskedAsync(fixture, fixture.ThirdId);

        var response = await SendAsync(HttpMethod.Get, RequestsPath(fixture), fixture.ThirdId);
        var found = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        Assert.Equal(1, found.GetArrayLength());
        Assert.True(found[0].GetProperty("isMine").GetBoolean());
    }

    // ---- Erbjudandet maste ga att fraga om --------------------------------------------

    [Fact]
    public async Task Fraga_PaTillbakadragetErbjudande_Avvisas()
    {
        // Den som frisatt sin plats ska inte fa fler forfragningar.
        var fixture = await SeedAsync("tillbakadraget");

        await SendAsync(
            HttpMethod.Post,
            $"/api/v1/matches/{fixture.MatchId}/carpool/offers/{fixture.OfferId}/withdraw",
            fixture.DriverId);

        var response = await AskAsync(fixture, fixture.AskerId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Fraga_PaSittEget_Avvisas()
    {
        var fixture = await SeedAsync("mitt-eget");

        var response = await AskAsync(fixture, fixture.DriverId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public async Task Platser_UtanforBilensTak_Avvisas(int seats)
    {
        var fixture = await SeedAsync($"platser-{seats}");

        var response = await AskAsync(fixture, fixture.AskerId, seats);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- Fritexten -------------------------------------------------------------------

    [Fact]
    public async Task Halsningen_HamnarAldrigIAuditloggen()
    {
        var fixture = await SeedAsync("audit");
        var requestId = await AskedAsync(fixture, fixture.AskerId);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var entries = await context.AuditEntries.AsNoTracking()
            .Where(e => e.SubjectId == requestId)
            .ToListAsync(CancellationToken.None);

        Assert.NotEmpty(entries);
        Assert.All(
            entries,
            entry => Assert.DoesNotContain(
                Greeting, entry.Details ?? string.Empty, StringComparison.Ordinal));
    }
}

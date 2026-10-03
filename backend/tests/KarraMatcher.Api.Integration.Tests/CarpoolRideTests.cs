using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using KarraMatcher.Application.Abstractions.Security;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Carpool;
using KarraMatcher.Domain.Children;
using KarraMatcher.Domain.Events;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Skjutsförfrågan (`#63`-spegeln, §KM.12): en förälder ber om skjuts, en förare erbjuder plats och
/// föräldern bekräftar. Vaktar flödet, objektnivå-ägarskapet och att ett nej bär ett meddelande.
/// </summary>
public sealed class CarpoolRideTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private static readonly DateTime Kickoff = new(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);

    private const string Note = "Vi bor vid Sandeslätt, behöver skjuts dit.";
    private const string DriverGreeting = "Jag kör och har plats, hänger ni på?";
    private const string DenyMessage = "Tyvärr, bilen blev full.";

    private static WebApplicationFactoryClientOptions ClientOptions => new() { HandleCookies = true };

    private sealed record Fixture(Guid MatchId, Guid RequesterId, Guid DriverId, Guid ThirdId);

    /// <summary>En match och tre medlemskonton, alla kallade så MemberOfEvent släpper in dem.</summary>
    private async Task<Fixture> SeedAsync(string suffix)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var club = new Club { Id = Guid.NewGuid(), Name = "Karra KIF", Slug = $"klubb-rr-{suffix}" };
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
            Slug = $"gul-rr-{suffix}",
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
        var match = new Event
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            KickoffUtc = Kickoff,
            OpponentName = "Torslanda",
            VenueId = venue.Id,
            IsHome = false,
            Status = EventStatus.Scheduled,
            IcsSequence = 0,
            UpdatedUtc = Kickoff,
        };

        var requester = new Account { Id = Guid.NewGuid(), Email = $"fragare-rr-{suffix}@example.com" };
        var driver = new Account { Id = Guid.NewGuid(), Email = $"forare-rr-{suffix}@example.com" };
        var third = new Account { Id = Guid.NewGuid(), Email = $"tredje-rr-{suffix}@example.com" };

        var accounts = new[] { requester, driver, third };
        var children = accounts
            .Select((_, i) => new Child
            {
                Id = Guid.NewGuid(),
                FirstName = "Barn",
                LastInitial = ((char)('A' + i)).ToString(),
                AgeGroupId = ageGroup.Id,
                TeamId = team.Id,
                CreatedUtc = Kickoff,
            })
            .ToArray();
        var guardianships = accounts
            .Select((account, i) => new Guardianship
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,
                ChildId = children[i].Id,
                GrantedUtc = Kickoff,
            });

        context.Clubs.Add(club);
        context.AgeGroups.Add(ageGroup);
        context.Teams.Add(team);
        context.Venues.Add(venue);
        context.Events.Add(match);
        context.Accounts.AddRange(accounts);
        context.Children.AddRange(children);
        context.Guardianships.AddRange(guardianships);

        AttendanceSeed.CallChildrenToMatch(
            context, match.Id, driver.Id, [.. children.Select(c => c.Id)]);

        await context.SaveChangesAsync(CancellationToken.None);

        return new Fixture(match.Id, requester.Id, driver.Id, third.Id);
    }

    private static string TokenFor(IServiceProvider services, Guid accountId)
    {
        using var scope = services.CreateScope();
        var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();

        return issuer.Issue(accountId, "foralder@example.com", new AccountRoles(false, [], [])).Token;
    }

    private static async Task<(string Token, string Cookie)> GetCsrfAsync(
        HttpClient client, string accessToken)
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
        HttpMethod method, string path, Guid actorId, object? payload = null)
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

    private static string RequestsPath(Fixture f) =>
        $"/api/v1/matches/{f.MatchId}/carpool/ride-requests";

    private async Task<Guid> CreateRequestAsync(Fixture f, Guid actorId, int seats = 1)
    {
        var response = await SendAsync(
            HttpMethod.Post, RequestsPath(f), actorId,
            new { direction = "Both", seats, note = Note });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        return body.GetProperty("id").GetGuid();
    }

    private async Task<Guid> OfferSeatAsync(Fixture f, Guid rideRequestId, Guid driverId)
    {
        var response = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/matches/{f.MatchId}/carpool/ride-requests/{rideRequestId}/offers",
            driverId,
            new { seats = 1, message = DriverGreeting });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        return body.GetProperty("id").GetGuid();
    }

    // ---- Kräver inloggning -----------------------------------------------------------

    [Fact]
    public async Task Skapa_UtanInloggning_Nekas()
    {
        var f = await SeedAsync("anon");

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            RequestsPath(f), new { direction = "Both", seats = 1, note = Note }, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Skapa + lista ---------------------------------------------------------------

    [Fact]
    public async Task Skapa_SomMedlem_SynsIListan()
    {
        var f = await SeedAsync("skapa");
        await CreateRequestAsync(f, f.RequesterId);

        var response = await SendAsync(HttpMethod.Get, RequestsPath(f), f.ThirdId);
        var found = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        Assert.Equal(1, found.GetArrayLength());
        Assert.Equal("Both", found[0].GetProperty("direction").GetString());
        Assert.Equal(Note, found[0].GetProperty("note").GetString());
        Assert.False(found[0].GetProperty("isMine").GetBoolean());
    }

    // ---- Platserbjudande + synlighet -------------------------------------------------

    [Fact]
    public async Task ErbjudPlats_SynsForFragarenMenInteForTredje()
    {
        var f = await SeedAsync("erbjud");
        var requestId = await CreateRequestAsync(f, f.RequesterId);
        await OfferSeatAsync(f, requestId, f.DriverId);

        var offersPath =
            $"/api/v1/matches/{f.MatchId}/carpool/ride-requests/{requestId}/offers";

        // Den som frågade ser platserbjudandet med förarens hälsning.
        var asRequester = await (await SendAsync(HttpMethod.Get, offersPath, f.RequesterId))
            .Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        Assert.Equal(1, asRequester.GetArrayLength());
        Assert.Equal(DriverGreeting, asRequester[0].GetProperty("message").GetString());

        // En tredje part ser inga platserbjudanden — fritexten är mellan de två (§KM.12).
        var asThird = await (await SendAsync(HttpMethod.Get, offersPath, f.ThirdId))
            .Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        Assert.Equal(0, asThird.GetArrayLength());
    }

    [Fact]
    public async Task ErbjudPlats_PaSinEgenForfragan_Avvisas()
    {
        var f = await SeedAsync("eget");
        var requestId = await CreateRequestAsync(f, f.RequesterId);

        var response = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/matches/{f.MatchId}/carpool/ride-requests/{requestId}/offers",
            f.RequesterId,
            new { seats = 1, message = DriverGreeting });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ErbjudPlats_TvaGanger_Avvisas()
    {
        var f = await SeedAsync("dubbelt");
        var requestId = await CreateRequestAsync(f, f.RequesterId);
        await OfferSeatAsync(f, requestId, f.DriverId);

        var second = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/matches/{f.MatchId}/carpool/ride-requests/{requestId}/offers",
            f.DriverId,
            new { seats = 1, message = DriverGreeting });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    // ---- Svar: accept / nej ----------------------------------------------------------

    [Fact]
    public async Task Acceptera_LoserForfragan_OchSvararFraganForaren()
    {
        var f = await SeedAsync("accept");
        var requestId = await CreateRequestAsync(f, f.RequesterId);
        var offerId = await OfferSeatAsync(f, requestId, f.DriverId);

        var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/matches/{f.MatchId}/carpool/ride-offers/{offerId}/accept",
            f.RequesterId, new { message = (string?)null });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var request = await context.CarpoolRideRequests.AsNoTracking()
            .SingleAsync(r => r.Id == requestId, CancellationToken.None);
        var offer = await context.CarpoolRideOffers.AsNoTracking()
            .SingleAsync(o => o.Id == offerId, CancellationToken.None);

        Assert.Equal(CarpoolRideRequestStatus.Fulfilled, request.Status);
        Assert.Equal(CarpoolRequestStatus.Accepted, offer.Status);

        // Löst förfrågan syns inte längre i listan.
        var listed = await (await SendAsync(HttpMethod.Get, RequestsPath(f), f.ThirdId))
            .Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        Assert.Equal(0, listed.GetArrayLength());
    }

    [Fact]
    public async Task Neka_UtanMeddelande_Avvisas()
    {
        var f = await SeedAsync("nej-tomt");
        var requestId = await CreateRequestAsync(f, f.RequesterId);
        var offerId = await OfferSeatAsync(f, requestId, f.DriverId);

        var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/matches/{f.MatchId}/carpool/ride-offers/{offerId}/deny",
            f.RequesterId, new { message = "" });

        // Ett tyst nej får inte förekomma (§KM.12).
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Neka_MedMeddelande_HallerForfraganOppen()
    {
        var f = await SeedAsync("nej");
        var requestId = await CreateRequestAsync(f, f.RequesterId);
        var offerId = await OfferSeatAsync(f, requestId, f.DriverId);

        var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/matches/{f.MatchId}/carpool/ride-offers/{offerId}/deny",
            f.RequesterId, new { message = DenyMessage });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var request = await context.CarpoolRideRequests.AsNoTracking()
            .SingleAsync(r => r.Id == requestId, CancellationToken.None);
        var offer = await context.CarpoolRideOffers.AsNoTracking()
            .SingleAsync(o => o.Id == offerId, CancellationToken.None);

        Assert.Equal(CarpoolRideRequestStatus.Open, request.Status);
        Assert.Equal(CarpoolRequestStatus.Denied, offer.Status);
        Assert.Equal(DenyMessage, offer.ResponseMessage);
    }

    [Fact]
    public async Task Svara_PaEnAnnansForfragan_Nekas()
    {
        // Objektnivå: bara den som äger skjutsförfrågan svarar på dess platserbjudanden.
        var f = await SeedAsync("fel-agare");
        var requestId = await CreateRequestAsync(f, f.RequesterId);
        var offerId = await OfferSeatAsync(f, requestId, f.DriverId);

        var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/matches/{f.MatchId}/carpool/ride-offers/{offerId}/accept",
            f.ThirdId, new { message = (string?)null });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Återta / dra tillbaka -------------------------------------------------------

    [Fact]
    public async Task Aterta_SomForare_Fungerar_MenInteSomNagonAnnan()
    {
        var f = await SeedAsync("aterta");
        var requestId = await CreateRequestAsync(f, f.RequesterId);
        var offerId = await OfferSeatAsync(f, requestId, f.DriverId);

        var byThird = await SendAsync(
            HttpMethod.Post, $"/api/v1/matches/{f.MatchId}/carpool/ride-offers/{offerId}/retract",
            f.ThirdId);
        Assert.Equal(HttpStatusCode.NotFound, byThird.StatusCode);

        var byDriver = await SendAsync(
            HttpMethod.Post, $"/api/v1/matches/{f.MatchId}/carpool/ride-offers/{offerId}/retract",
            f.DriverId);
        Assert.Equal(HttpStatusCode.NoContent, byDriver.StatusCode);
    }

    [Fact]
    public async Task DraTillbaka_SomFragare_Fungerar_MenInteSomNagonAnnan()
    {
        var f = await SeedAsync("dratillbaka");
        var requestId = await CreateRequestAsync(f, f.RequesterId);

        var byThird = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/matches/{f.MatchId}/carpool/ride-requests/{requestId}/withdraw",
            f.ThirdId);
        Assert.Equal(HttpStatusCode.NotFound, byThird.StatusCode);

        var byOwner = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/matches/{f.MatchId}/carpool/ride-requests/{requestId}/withdraw",
            f.RequesterId);
        Assert.Equal(HttpStatusCode.NoContent, byOwner.StatusCode);

        // En tillbakadragen förfrågan går inte längre att erbjuda plats på.
        var offer = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/matches/{f.MatchId}/carpool/ride-requests/{requestId}/offers",
            f.DriverId,
            new { seats = 1, message = DriverGreeting });
        Assert.Equal(HttpStatusCode.NotFound, offer.StatusCode);
    }

    // ---- Fritexten -------------------------------------------------------------------

    [Fact]
    public async Task Notisen_HamnarAldrigIAuditloggen()
    {
        var f = await SeedAsync("audit");
        var requestId = await CreateRequestAsync(f, f.RequesterId);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var entries = await context.AuditEntries.AsNoTracking()
            .Where(e => e.SubjectId == requestId)
            .ToListAsync(CancellationToken.None);

        Assert.NotEmpty(entries);
        Assert.All(
            entries,
            entry => Assert.DoesNotContain(
                Note, entry.Details ?? string.Empty, StringComparison.Ordinal));
    }
}

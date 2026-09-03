using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using KarraMatcher.Application.Abstractions.Security;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Audit;
using KarraMatcher.Domain.Carpool;
using KarraMatcher.Domain.Matches;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Samåkningserbjudandet (§KM.12, §KM.10, §KM.3).
///
/// <para>
/// Appens enda funktion där föräldrar gör något med varandra. Fyra saker vaktas: att det
/// krävs konto för att lägga upp, att platserna hålls inom 1–4, att bara ägaren kan dra
/// tillbaka sitt eget — och att förarens fritext varken loggas eller lämnas ut till någon
/// som inte är inloggad.
/// </para>
/// </summary>
public sealed class CarpoolOfferTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private static readonly DateTime Kickoff = new(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime Departure = new(2026, 9, 20, 10, 30, 0, DateTimeKind.Utc);

    private const string Note = "Vi har plats för en väska till, hör av dig så löser vi det";

    private static WebApplicationFactoryClientOptions ClientOptions => new() { HandleCookies = true };

    private sealed record Fixture(Guid MatchId, Guid DriverId, Guid OtherId);

    /// <summary>En match och två konton — föraren och någon annan.</summary>
    private async Task<Fixture> SeedAsync(string suffix)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var club = new Club { Id = Guid.NewGuid(), Name = "Karra KIF", Slug = $"klubb-{suffix}" };
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
            Slug = $"gul-{suffix}",
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

        // Kontona behovs pa riktigt: erbjudandet har en frammande nyckel till Accounts, sa
        // att en radering tar med sig det kontot lagt upp (§KM.6).
        var driver = new Account { Id = Guid.NewGuid(), Email = $"forare-{suffix}@example.com" };
        var other = new Account { Id = Guid.NewGuid(), Email = $"annan-{suffix}@example.com" };

        context.Clubs.Add(club);
        context.AgeGroups.Add(ageGroup);
        context.Teams.Add(team);
        context.Venues.Add(venue);
        context.Matches.Add(match);
        context.Accounts.AddRange(driver, other);

        await context.SaveChangesAsync(CancellationToken.None);

        return new Fixture(match.Id, driver.Id, other.Id);
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

    /// <summary>Ett anrop som en inloggad förälder.</summary>
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

    private static object Offer(int seats = 3, string? note = Note) => new
    {
        direction = "Both",
        departurePlace = "Karra centrum",
        departureUtc = Departure,
        seats,
        note,
    };

    private async Task<Guid> CreateOfferAsync(Fixture fixture, int seats = 3, string? note = Note)
    {
        var response = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/matches/{fixture.MatchId}/carpool/offers",
            fixture.DriverId,
            Offer(seats, note));

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        return body.GetProperty("id").GetGuid();
    }

    // ---- Kraver inloggning -----------------------------------------------------------

    [Fact]
    public async Task LaggaUpp_UtanInloggning_Nekas()
    {
        var fixture = await SeedAsync("anon");

        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/matches/{fixture.MatchId}/carpool/offers",
            Offer(),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DraTillbaka_UtanInloggning_Nekas()
    {
        var fixture = await SeedAsync("anon-withdraw");
        var offerId = await CreateOfferAsync(fixture);

        using var client = factory.CreateClient();

        var response = await client.PostAsync(
            $"/api/v1/matches/{fixture.MatchId}/carpool/offers/{offerId}/withdraw",
            content: null,
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Platserna ar 1-4 ------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public async Task Platser_InomGransen_Accepteras(int seats)
    {
        var fixture = await SeedAsync($"seats-ok-{seats}");

        var response = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/matches/{fixture.MatchId}/carpool/offers",
            fixture.DriverId,
            Offer(seats));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(-1)]
    [InlineData(40)]
    public async Task Platser_UtanforGransen_Avvisas(int seats)
    {
        /*
         * Fyra ar taket for att det ar vad som far plats i en vanlig bil utover foraren och
         * det egna barnet. Grasen provas server-side -- ett formular ar UX, inte en regel.
         */
        var fixture = await SeedAsync($"seats-nej-{seats}");

        var response = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/matches/{fixture.MatchId}/carpool/offers",
            fixture.DriverId,
            Offer(seats));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- Bara agaren drar tillbaka ---------------------------------------------------

    [Fact]
    public async Task DraTillbaka_SomAgare_Fungerar()
    {
        var fixture = await SeedAsync("agare");
        var offerId = await CreateOfferAsync(fixture);

        var response = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/matches/{fixture.MatchId}/carpool/offers/{offerId}/withdraw",
            fixture.DriverId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DraTillbaka_SomNagonAnnan_Nekas()
    {
        /*
         * Objektnivå-auktorisering (checklistan 2.6). Den andra foraldern har ett giltigt
         * konto, en giltig token och ett giltigt anrop -- och ska anda nekas.
         */
        var fixture = await SeedAsync("inte-agare");
        var offerId = await CreateOfferAsync(fixture);

        var response = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/matches/{fixture.MatchId}/carpool/offers/{offerId}/withdraw",
            fixture.OtherId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        var offer = await context.CarpoolOffers.AsNoTracking()
            .SingleAsync(o => o.Id == offerId, CancellationToken.None);

        Assert.Equal(CarpoolOfferStatus.Open, offer.Status);
    }

    [Fact]
    public async Task DraTillbaka_SomNagonAnnan_SvararSammaSomForEttOkantId()
    {
        /*
         * Skulle svaren skilja sig kunde vem som helst rakna ut vilka erbjudande-id som
         * existerar genom att prova sig fram.
         */
        var fixture = await SeedAsync("gissa");
        var offerId = await CreateOfferAsync(fixture);

        var mine = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/matches/{fixture.MatchId}/carpool/offers/{offerId}/withdraw",
            fixture.OtherId);

        var nonsense = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/matches/{fixture.MatchId}/carpool/offers/{Guid.NewGuid()}/withdraw",
            fixture.OtherId);

        Assert.Equal(nonsense.StatusCode, mine.StatusCode);
    }

    // ---- Tillbakadraget syns inte som bokningsbart ------------------------------------

    [Fact]
    public async Task Tillbakadraget_ForsvinnerUrListan()
    {
        var fixture = await SeedAsync("borta");
        var offerId = await CreateOfferAsync(fixture);

        using var client = factory.CreateClient();

        var before = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/matches/{fixture.MatchId}/carpool/offers",
            CancellationToken.None);

        Assert.Equal(1, before.GetArrayLength());

        await SendAsync(
            HttpMethod.Post,
            $"/api/v1/matches/{fixture.MatchId}/carpool/offers/{offerId}/withdraw",
            fixture.DriverId);

        var after = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/matches/{fixture.MatchId}/carpool/offers",
            CancellationToken.None);

        Assert.Equal(0, after.GetArrayLength());
    }

    [Fact]
    public async Task Tillbakadraget_RaderasInte()
    {
        /*
         * Raden ligger kvar tills gallringen tar hela matchens samakning 30 dagar efterat
         * (§KM.12). Den som fragat ska kunna se vad som hande med sin forfragan, inte mota
         * ett tomt hal.
         */
        var fixture = await SeedAsync("kvar");
        var offerId = await CreateOfferAsync(fixture);

        await SendAsync(
            HttpMethod.Post,
            $"/api/v1/matches/{fixture.MatchId}/carpool/offers/{offerId}/withdraw",
            fixture.DriverId);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        var offer = await context.CarpoolOffers.AsNoTracking()
            .SingleAsync(o => o.Id == offerId, CancellationToken.None);

        Assert.Equal(CarpoolOfferStatus.Withdrawn, offer.Status);
    }

    // ---- Fritexten -------------------------------------------------------------------

    [Fact]
    public async Task Notisen_HamnarAldrigIAuditloggen()
    {
        // §KM.10 och §KM.12: fritext loggas aldrig. Audit-raden ska bara veta vad som hande.
        var fixture = await SeedAsync("audit");
        var offerId = await CreateOfferAsync(fixture);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var entries = await context.AuditEntries.AsNoTracking()
            .Where(e => e.SubjectId == offerId)
            .ToListAsync(CancellationToken.None);

        Assert.NotEmpty(entries);
        Assert.All(entries, entry => Assert.DoesNotContain(Note, entry.Details ?? string.Empty, StringComparison.Ordinal));
        Assert.Contains(entries, entry => entry.Action == AuditActions.CarpoolOfferCreated);
    }

    [Fact]
    public async Task Notisen_LamnasInteUtTillEnGast()
    {
        /*
         * Erbjudandet far ses av vem som helst (§KM.3), men fritext ar potentiell PII och
         * ska bara na de inblandade (§KM.12). Gasten far alltsa erbjudandet utan notisen --
         * inte ett avslag.
         */
        var fixture = await SeedAsync("gast");
        await CreateOfferAsync(fixture);

        using var client = factory.CreateClient();

        var offers = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/matches/{fixture.MatchId}/carpool/offers",
            CancellationToken.None);

        var first = offers[0];

        Assert.Equal(JsonValueKind.Null, first.GetProperty("note").ValueKind);
        Assert.Equal(3, first.GetProperty("seats").GetInt32());
        Assert.Equal("Karra centrum", first.GetProperty("departurePlace").GetString());
        Assert.False(first.GetProperty("isMine").GetBoolean());
    }

    [Fact]
    public async Task Notisen_SynsForEnInloggad()
    {
        var fixture = await SeedAsync("inloggad");
        await CreateOfferAsync(fixture);

        var response = await SendAsync(
            HttpMethod.Get,
            $"/api/v1/matches/{fixture.MatchId}/carpool/offers",
            fixture.OtherId);

        var offers = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        Assert.Equal(Note, offers[0].GetProperty("note").GetString());
        Assert.False(offers[0].GetProperty("isMine").GetBoolean());
    }

    [Fact]
    public async Task Foraren_SerSittEgetSomSitt()
    {
        var fixture = await SeedAsync("mitt");
        await CreateOfferAsync(fixture);

        var response = await SendAsync(
            HttpMethod.Get,
            $"/api/v1/matches/{fixture.MatchId}/carpool/offers",
            fixture.DriverId);

        var offers = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        Assert.True(offers[0].GetProperty("isMine").GetBoolean());
    }

    // ---- Svaret far inte ligga i en delad cache ---------------------------------------

    [Fact]
    public async Task Listan_ArAldrigPubliktCachebar()
    {
        /*
         * Svaret innehaller olika mycket beroende pa vem som fragar. Hamnade det i Vercels
         * edge kunde en foralders fritext levereras till nagon annan.
         */
        var fixture = await SeedAsync("cache");

        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/v1/matches/{fixture.MatchId}/carpool/offers",
            CancellationToken.None);

        var cacheControl = response.Headers.CacheControl?.ToString() ?? string.Empty;

        Assert.DoesNotContain("s-maxage", cacheControl, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("public", cacheControl, StringComparison.OrdinalIgnoreCase);
    }

    // ---- Matchen maste finnas ---------------------------------------------------------

    [Fact]
    public async Task Erbjudande_PaEnMatchSomInteFinns_Avvisas()
    {
        var fixture = await SeedAsync("ingen-match");

        var response = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/matches/{Guid.NewGuid()}/carpool/offers",
            fixture.DriverId,
            Offer());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

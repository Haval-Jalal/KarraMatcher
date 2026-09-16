using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using KarraMatcher.Application.Abstractions.Security;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Carpool;
using KarraMatcher.Domain.Children;
using KarraMatcher.Domain.Matches;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Förarens svar på en åkförfrågan (§KM.12).
///
/// <para>
/// Tre regler prövas här, och alla tre server-side: bara erbjudandets ägare får svara
/// (checklistan 2.8), en accept som spränger antalet platser avvisas (2.9), och ett nekande
/// utan meddelande avvisas (2.10). Den sista är inte artighet — det är en granne man möter
/// på planen nästa lördag.
/// </para>
/// </summary>
public sealed class CarpoolResponseTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private static readonly DateTime Kickoff = new(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);

    private const string Greeting = "Hej! Går det bra att vi hakar på?";

    private const string Refusal = "Ändrade planer, kan tyvärr inte köra.";

    private static WebApplicationFactoryClientOptions ClientOptions => new() { HandleCookies = true };

    private sealed record Fixture(Guid MatchId, Guid OfferId, Guid DriverId, Guid AskerId, Guid ThirdId);

    /// <summary>En match, ett erbjudande, och tre konton.</summary>
    private async Task<Fixture> SeedAsync(string suffix, int seats = 1)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var club = new Club { Id = Guid.NewGuid(), Name = "Karra KIF", Slug = $"klubb-s-{suffix}" };
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
            Slug = $"gul-s-{suffix}",
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

        var driver = new Account { Id = Guid.NewGuid(), Email = $"forare-s-{suffix}@example.com" };
        var asker = new Account { Id = Guid.NewGuid(), Email = $"fragare-s-{suffix}@example.com" };
        var third = new Account { Id = Guid.NewGuid(), Email = $"tredje-s-{suffix}@example.com" };

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

        // Alla tre ar medlemmar av laget (v2, §KM.3): vardnadshavare till varsitt barn i det.
        // Utan medlemskap kan de inte se matchens samakningslista.
        var children = new[] { driver, asker, third }
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
        var guardianships = new[] { driver, asker, third }
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
        context.Matches.Add(match);
        context.Accounts.AddRange(driver, asker, third);
        context.Children.AddRange(children);
        context.Guardianships.AddRange(guardianships);
        context.CarpoolOffers.Add(offer);

        await context.SaveChangesAsync(CancellationToken.None);

        return new Fixture(match.Id, offer.Id, driver.Id, asker.Id, third.Id);
    }

    private static string TokenFor(IServiceProvider services, Guid accountId)
    {
        using var scope = services.CreateScope();
        var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();

        return issuer.Issue(accountId, "foralder@example.com", new AccountRoles(false, [], [])).Token;
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

    private async Task<Guid> AskedAsync(Fixture fixture, Guid actorId, int seats = 1)
    {
        var response = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/matches/{fixture.MatchId}/carpool/offers/{fixture.OfferId}/requests",
            actorId,
            new { seats, message = Greeting });

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        return body.GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> AcceptAsync(Fixture fixture, Guid requestId, Guid actorId) =>
        SendAsync(
            HttpMethod.Post,
            $"/api/v1/matches/{fixture.MatchId}/carpool/requests/{requestId}/accept",
            actorId,
            new { message = (string?)null });

    private Task<HttpResponseMessage> DenyAsync(
        Fixture fixture,
        Guid requestId,
        Guid actorId,
        string? message = Refusal) =>
        SendAsync(
            HttpMethod.Post,
            $"/api/v1/matches/{fixture.MatchId}/carpool/requests/{requestId}/deny",
            actorId,
            new { message });

    private async Task<CarpoolRequest> StoredAsync(Guid requestId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        return await context.CarpoolRequests.AsNoTracking()
            .SingleAsync(r => r.Id == requestId, CancellationToken.None);
    }

    // ---- Bara agaren far svara (checklistan 2.8) --------------------------------------

    [Fact]
    public async Task Acceptera_UtanInloggning_Nekas()
    {
        var fixture = await SeedAsync("anon");
        var requestId = await AskedAsync(fixture, fixture.AskerId);

        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/matches/{fixture.MatchId}/carpool/requests/{requestId}/accept",
            new { message = (string?)null },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Neka_UtanInloggning_Nekas()
    {
        /*
         * Ett nekande andrar tillstand och kraver darfor konto (§KM.3). Att acceptera redan
         * har ett sadant test racker inte -- de gar genom varsin endpoint, och den dag nagon
         * flyttar ett attribut ar det bara det anropet som gors om.
         */
        var fixture = await SeedAsync("anon-neka");
        var requestId = await AskedAsync(fixture, fixture.AskerId);

        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/matches/{fixture.MatchId}/carpool/requests/{requestId}/deny",
            new { message = Refusal },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Acceptera_SomNagonAnnan_Nekas()
    {
        /*
         * En tredje foralder forsoker slappa in nagon i en bil som inte ar hens. Svaret ar
         * detsamma som for en forfragan som inte finns -- annars gar det att kartlagga vilka
         * id som existerar genom att prova sig fram.
         */
        var fixture = await SeedAsync("fel-agare");
        var requestId = await AskedAsync(fixture, fixture.AskerId);

        var response = await AcceptAsync(fixture, requestId, fixture.ThirdId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(CarpoolRequestStatus.Pending, (await StoredAsync(requestId)).Status);
    }

    [Fact]
    public async Task Acceptera_SinEgenForfragan_Nekas()
    {
        // Aven den som fragat sjalv far ett nej. Det ar foraren som valjer.
        var fixture = await SeedAsync("egen");
        var requestId = await AskedAsync(fixture, fixture.AskerId);

        var response = await AcceptAsync(fixture, requestId, fixture.AskerId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(CarpoolRequestStatus.Pending, (await StoredAsync(requestId)).Status);
    }

    // ---- Platsrakning server-side (checklistan 2.9) -----------------------------------

    [Fact]
    public async Task Acceptera_SomForare_Fungerar()
    {
        var fixture = await SeedAsync("ja", seats: 2);
        var requestId = await AskedAsync(fixture, fixture.AskerId, seats: 2);

        var response = await AcceptAsync(fixture, requestId, fixture.DriverId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(CarpoolRequestStatus.Accepted, (await StoredAsync(requestId)).Status);
    }

    [Fact]
    public async Task Acceptera_FlerPlatserAnBilenHar_Avvisas()
    {
        /*
         * Erbjudandet har en plats, forfragan galler fyra. Att fraga fick ga igenom (#51);
         * det ar accepten som rakar med platserna.
         */
        var fixture = await SeedAsync("for-manga", seats: 1);
        var requestId = await AskedAsync(fixture, fixture.AskerId, seats: 4);

        var response = await AcceptAsync(fixture, requestId, fixture.DriverId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(CarpoolRequestStatus.Pending, (await StoredAsync(requestId)).Status);
    }

    [Fact]
    public async Task Acceptera_NarPlatsernaRedanTagitSlut_Avvisas()
    {
        // Tva platser, tva redan accepterade. Nasta ja skulle spranga bilen.
        var fixture = await SeedAsync("slut", seats: 2);

        var first = await AskedAsync(fixture, fixture.AskerId, seats: 2);
        (await AcceptAsync(fixture, first, fixture.DriverId)).EnsureSuccessStatusCode();

        var second = await AskedAsync(fixture, fixture.ThirdId, seats: 1);

        var response = await AcceptAsync(fixture, second, fixture.DriverId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(CarpoolRequestStatus.Pending, (await StoredAsync(second)).Status);
    }

    [Fact]
    public async Task Neka_NarBilenArFull_GarBra()
    {
        /*
         * Poangen med hela upplagget: nar platserna ar slut ska foraren fortfarande kunna
         * svara "nagon annan hann fore" i stallet for att forfragan blir liggande.
         */
        var fixture = await SeedAsync("full-nej", seats: 1);

        var first = await AskedAsync(fixture, fixture.AskerId);
        (await AcceptAsync(fixture, first, fixture.DriverId)).EnsureSuccessStatusCode();

        var second = await AskedAsync(fixture, fixture.ThirdId);

        var response = await DenyAsync(
            fixture, second, fixture.DriverId, "Någon annan hann före, tyvärr!");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(CarpoolRequestStatus.Denied, (await StoredAsync(second)).Status);
    }

    // ---- Ett nekande kraver ord (checklistan 2.10) ------------------------------------

    [Fact]
    public async Task Neka_MedMeddelande_Fungerar()
    {
        var fixture = await SeedAsync("nej");
        var requestId = await AskedAsync(fixture, fixture.AskerId);

        var response = await DenyAsync(fixture, requestId, fixture.DriverId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var stored = await StoredAsync(requestId);

        Assert.Equal(CarpoolRequestStatus.Denied, stored.Status);
        Assert.Equal(Refusal, stored.ResponseMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Neka_UtanMeddelande_Avvisas(string? message)
    {
        // Ett tyst nej far inte forekomma. Blanktecken raknas inte som ord.
        var fixture = await SeedAsync($"tyst-{message?.Length ?? 0}-{message is null}");
        var requestId = await AskedAsync(fixture, fixture.AskerId);

        var response = await DenyAsync(fixture, requestId, fixture.DriverId, message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(CarpoolRequestStatus.Pending, (await StoredAsync(requestId)).Status);
    }

    [Fact]
    public async Task Neka_MedForLangtMeddelande_Avvisas()
    {
        var fixture = await SeedAsync("langt");
        var requestId = await AskedAsync(fixture, fixture.AskerId);

        var response = await DenyAsync(fixture, requestId, fixture.DriverId, new string('a', 501));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- Tillstandsovergangarna (§KM.12) ----------------------------------------------

    [Fact]
    public async Task Svara_TvaGanger_Avvisas()
    {
        /*
         * Till skillnad fran att atertaga, dar ett andra forsok ar ofarligt, betyder ett
         * andra svar att foraren andrar sig -- och det ar inte samma handelse.
         */
        var fixture = await SeedAsync("tva-svar");
        var requestId = await AskedAsync(fixture, fixture.AskerId);

        (await AcceptAsync(fixture, requestId, fixture.DriverId)).EnsureSuccessStatusCode();

        var again = await DenyAsync(fixture, requestId, fixture.DriverId);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(CarpoolRequestStatus.Accepted, (await StoredAsync(requestId)).Status);
    }

    [Fact]
    public async Task Svara_PaAtertagenForfragan_Avvisas()
    {
        var fixture = await SeedAsync("atertagen");
        var requestId = await AskedAsync(fixture, fixture.AskerId);

        await SendAsync(
            HttpMethod.Post,
            $"/api/v1/matches/{fixture.MatchId}/carpool/requests/{requestId}/retract",
            fixture.AskerId);

        var response = await AcceptAsync(fixture, requestId, fixture.DriverId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(CarpoolRequestStatus.Retracted, (await StoredAsync(requestId)).Status);
    }

    [Fact]
    public async Task Svara_PaTillbakadragetErbjudande_Avvisas()
    {
        var fixture = await SeedAsync("tillbakadraget");
        var requestId = await AskedAsync(fixture, fixture.AskerId);

        await SendAsync(
            HttpMethod.Post,
            $"/api/v1/matches/{fixture.MatchId}/carpool/offers/{fixture.OfferId}/withdraw",
            fixture.DriverId);

        var response = await AcceptAsync(fixture, requestId, fixture.DriverId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(CarpoolRequestStatus.Pending, (await StoredAsync(requestId)).Status);
    }

    // ---- Fullt erbjudande syns, det gommer sig inte -----------------------------------

    [Fact]
    public async Task Erbjudandet_ArFullt_NarPlatsernaTagitSlut()
    {
        var fixture = await SeedAsync("markt", seats: 2);
        var requestId = await AskedAsync(fixture, fixture.AskerId, seats: 2);

        (await AcceptAsync(fixture, requestId, fixture.DriverId)).EnsureSuccessStatusCode();

        var response = await SendAsync(
            HttpMethod.Get,
            $"/api/v1/matches/{fixture.MatchId}/carpool/offers",
            fixture.AskerId);
        var listed = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        // Kvar i listan, inte bortfiltrerat -- annars kan ingen fraga om en avhoppad plats.
        Assert.Equal(1, listed.GetArrayLength());
        Assert.True(listed[0].GetProperty("isFull").GetBoolean());
        Assert.Equal(0, listed[0].GetProperty("seatsLeft").GetInt32());
        Assert.Equal(2, listed[0].GetProperty("seatsTaken").GetInt32());
    }

    [Fact]
    public async Task Erbjudandet_ArInteFullt_AvEnForfraganSomBaraVantar()
    {
        // Att fraga tar ingen plats i ansprak. Bara accepterade raknas.
        var fixture = await SeedAsync("vantar", seats: 1);

        await AskedAsync(fixture, fixture.AskerId);

        var response = await SendAsync(
            HttpMethod.Get,
            $"/api/v1/matches/{fixture.MatchId}/carpool/offers",
            fixture.AskerId);
        var listed = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        Assert.False(listed[0].GetProperty("isFull").GetBoolean());
        Assert.Equal(1, listed[0].GetProperty("seatsLeft").GetInt32());
    }

    [Fact]
    public async Task Erbjudandet_GarAttFragaOm_AvenNarDetArFullt()
    {
        var fixture = await SeedAsync("fraga-fullt", seats: 1);

        var first = await AskedAsync(fixture, fixture.AskerId);
        (await AcceptAsync(fixture, first, fixture.DriverId)).EnsureSuccessStatusCode();

        var response = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/matches/{fixture.MatchId}/carpool/offers/{fixture.OfferId}/requests",
            fixture.ThirdId,
            new { seats = 1, message = Greeting });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ---- Fritexten (§KM.10, §KM.12) ---------------------------------------------------

    [Fact]
    public async Task Svaret_SynsForDenSomFragade()
    {
        var fixture = await SeedAsync("ser-svaret");
        var requestId = await AskedAsync(fixture, fixture.AskerId);

        await DenyAsync(fixture, requestId, fixture.DriverId);

        var response = await SendAsync(
            HttpMethod.Get,
            $"/api/v1/matches/{fixture.MatchId}/carpool/offers/{fixture.OfferId}/requests",
            fixture.AskerId);

        var found = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        Assert.Equal(Refusal, found[0].GetProperty("responseMessage").GetString());
    }

    [Fact]
    public async Task Forfragningarna_GarAttHamtaUtanCsrfToken()
    {
        /*
         * Regression (#71). En GET andrar inget och ska aldrig krava en CSRF-token. Klienten
         * skickar bara Bearer pa lasningar (getAuthJson), men forarens lista over
         * forfragningar satt bakom [RequireCsrfToken] pa CarpoolDriverController -- pa
         * klassniva, sa aven GET:en traffades. Foljden: en forare kunde aldrig hamta sina
         * inkommande forfragningar i drift. De ovriga testerna dolde det genom att skicka en
         * CSRF-token aven pa sina GET-anrop, vilket den riktiga klienten aldrig gor.
         */
        var fixture = await SeedAsync("get-utan-csrf");
        await AskedAsync(fixture, fixture.AskerId);

        using var client = factory.CreateClient(ClientOptions);

        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/matches/{fixture.MatchId}/carpool/offers/{fixture.OfferId}/requests");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", TokenFor(factory.Services, fixture.DriverId));

        // Medvetet ingen X-CSRF-TOKEN -- det är just det klienten inte skickar på en GET.
        var response = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Svaret_HamnarAldrigIAuditloggen()
    {
        // §KM.10: raden bar vad som hande och vem, aldrig foralderns egna ord.
        var fixture = await SeedAsync("audit");
        var requestId = await AskedAsync(fixture, fixture.AskerId);

        await DenyAsync(fixture, requestId, fixture.DriverId);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var entries = await context.AuditEntries.AsNoTracking()
            .ToListAsync(CancellationToken.None);

        Assert.Contains(entries, e => e.Action == "samakning.forfragan.nekad");
        Assert.DoesNotContain(
            entries,
            e => e.Details is not null && e.Details.Contains(Refusal, StringComparison.Ordinal));
    }
}

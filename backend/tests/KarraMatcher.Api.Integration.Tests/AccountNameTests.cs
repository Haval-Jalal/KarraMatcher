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
/// Namnet på kontot (`#154`).
///
/// <para>
/// Namnet är en personuppgift om en <b>vuxen</b> — §KM.1:s tak gäller barn och berörs
/// inte. Men det får bara nå laget, aldrig internet, och aldrig en logg. De tre gränserna
/// är vad som prövas här.
/// </para>
/// </summary>
public sealed class AccountNameTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private const string FirstName = "Anna";
    private const string LastName = "Bergstrom";

    private static WebApplicationFactoryClientOptions ClientOptions => new() { HandleCookies = true };

    private sealed record Fixture(Guid MatchId, Guid DriverId);

    /// <summary>En match med ett erbjudande, och en förare med namn.</summary>
    private async Task<Fixture> SeedAsync(string suffix, bool named = true)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var now = DateTime.UtcNow;

        var club = new Club { Id = Guid.NewGuid(), Name = "Karra KIF", Slug = $"klubb-n-{suffix}" };
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
            Slug = $"gul-n-{suffix}",
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
            KickoffUtc = now.AddDays(4),
            OpponentName = "Torslanda",
            VenueId = venue.Id,
            IsHome = false,
            Status = EventStatus.Scheduled,
            IcsSequence = 0,
            UpdatedUtc = now,
        };

        var driver = new Account
        {
            Id = Guid.NewGuid(),
            Email = $"forare-n-{suffix}@example.com",
            FirstName = named ? FirstName : null,
            LastName = named ? LastName : null,
            CreatedUtc = now,
        };

        var offer = new CarpoolOffer
        {
            Id = Guid.NewGuid(),
            MatchId = match.Id,
            DriverAccountId = driver.Id,
            Direction = CarpoolDirection.ToMatch,
            DeparturePlace = "Karra centrum",
            DepartureUtc = match.KickoffUtc.AddHours(-1),
            Seats = 3,
            Status = CarpoolOfferStatus.Open,
            CreatedUtc = now,
            UpdatedUtc = now,
        };

        // Foraren ar medlem av laget (v2, §KM.3): vardnadshavare till ett barn i det. Utan
        // medlemskap kan hen inte se matchens samakningslista.
        var child = new Child
        {
            Id = Guid.NewGuid(),
            FirstName = "Liam",
            LastInitial = "J",
            AgeGroupId = ageGroup.Id,
            TeamId = team.Id,
            CreatedUtc = now,
        };

        context.Clubs.Add(club);
        context.AgeGroups.Add(ageGroup);
        context.Teams.Add(team);
        context.Venues.Add(venue);
        context.Events.Add(match);
        context.Accounts.Add(driver);
        context.Children.Add(child);
        context.Guardianships.Add(new Guardianship
        {
            Id = Guid.NewGuid(),
            AccountId = driver.Id,
            ChildId = child.Id,
            GrantedUtc = now,
        });
        context.CarpoolOffers.Add(offer);

        await context.SaveChangesAsync(CancellationToken.None);

        return new Fixture(match.Id, driver.Id);
    }

    private string TokenFor(Guid accountId)
    {
        using var scope = factory.Services.CreateScope();
        var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();

        return issuer.Issue(accountId, "konto@example.com", new AccountRoles(false, [], [])).Token;
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

    private async Task<HttpResponseMessage> PutNameAsync(Guid accountId, object payload)
    {
        var token = TokenFor(accountId);

        using var client = factory.CreateClient(ClientOptions);
        var (csrf, cookie) = await GetCsrfAsync(client, token);

        var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/auth/profile")
        {
            Content = JsonContent.Create(payload),
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        request.Headers.Add("Cookie", cookie);

        return await client.SendAsync(request, CancellationToken.None);
    }

    private async Task<HttpResponseMessage> GetAsync(string path, Guid? accountId)
    {
        using var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, path);

        if (accountId is not null)
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", TokenFor(accountId.Value));
        }

        return await client.SendAsync(request, CancellationToken.None);
    }

    // ---- Namnet nar aldrig en gast ----------------------------------------------------

    [Fact]
    public async Task Erbjudande_ForEnGast_Nekas()
    {
        /*
         * Karnan i #154:s integritetsdel, skarpt i v2 (§KM.3, #191). Erbjudandelistan var
         * forr oppen och baregde inget namn; nu ar hela listan stangd, sa foraldrarnas namn
         * kan aldrig na nagon utanfor laget -- en gast far 401.
         */
        var fixture = await SeedAsync("gast");

        var response = await GetAsync($"/api/v1/matches/{fixture.MatchId}/carpool/offers", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Erbjudande_ForEnInloggad_BarForarensNamn()
    {
        var fixture = await SeedAsync("inloggad");

        var response = await GetAsync(
            $"/api/v1/matches/{fixture.MatchId}/carpool/offers",
            fixture.DriverId);

        response.EnsureSuccessStatusCode();

        var offers = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        var driverName = offers.EnumerateArray().First().GetProperty("driverName").GetString();

        Assert.Equal($"{FirstName} {LastName}", driverName);
    }

    [Fact]
    public async Task Erbjudande_FranKontoUtanNamn_BarInget()
    {
        // Konton som fanns fore #154 har inget namn. Det ska bli null, inte en tom strang
        // som ser ut som ett namn i granssnittet.
        var fixture = await SeedAsync("namnlos", named: false);

        var response = await GetAsync(
            $"/api/v1/matches/{fixture.MatchId}/carpool/offers",
            fixture.DriverId);

        response.EnsureSuccessStatusCode();

        var offers = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        var driverName = offers.EnumerateArray().First().GetProperty("driverName");

        Assert.Equal(JsonValueKind.Null, driverName.ValueKind);
    }

    // ---- Att satta sitt namn ----------------------------------------------------------

    [Fact]
    public async Task Namn_UtanInloggning_Nekas()
    {
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            "/api/v1/auth/profile",
            new { firstName = FirstName, lastName = LastName },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Namn_UtanFornamn_Avvisas()
    {
        // Efternamnet ar valfritt, fornamnet inte. Ett konto utan fornamn har inget att
        // visa, och da ar samakningen tillbaka dar den var fore #154.
        var fixture = await SeedAsync("utan-fornamn");

        var response = await PutNameAsync(fixture.DriverId, new { firstName = "  ", lastName = "Bergstrom" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Namn_UtanEfternamn_Accepteras()
    {
        var fixture = await SeedAsync("bara-fornamn", named: false);

        var response = await PutNameAsync(fixture.DriverId, new { firstName = "Johan", lastName = (string?)null });

        response.EnsureSuccessStatusCode();

        var profile = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        Assert.Equal("Johan", profile.GetProperty("displayName").GetString());
        Assert.False(profile.GetProperty("needsName").GetBoolean());
    }

    [Fact]
    public async Task Namn_ForLangt_Avvisas()
    {
        var fixture = await SeedAsync("for-langt");

        var response = await PutNameAsync(
            fixture.DriverId,
            new { firstName = new string('a', 61), lastName = (string?)null });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Profil_ForEttKontoUtanNamn_SagerAttDetSaknas()
    {
        // Granssnittet fragar efter namnet vid nasta inloggning, och det har ar signalen.
        var fixture = await SeedAsync("saknas", named: false);

        var response = await GetAsync("/api/v1/auth/profile", fixture.DriverId);

        response.EnsureSuccessStatusCode();

        var profile = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        Assert.True(profile.GetProperty("needsName").GetBoolean());
    }

    [Fact]
    public async Task Profil_UtanInloggning_Nekas()
    {
        var response = await GetAsync("/api/v1/auth/profile", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Namnet hamnar aldrig i auditloggen -------------------------------------------

    [Fact]
    public async Task Namnandring_SkriverIngetNamnIAuditloggen()
    {
        /*
         * §KM.10. Audit-raden ligger kvar aven nar kontot raderats -- ett namn dar hade
         * gjort raderingen ofullstandig, och det ar precis den sortens lacka som ingen
         * upptacker forran nagon begar ett registerutdrag.
         */
        var fixture = await SeedAsync("audit", named: false);

        var response = await PutNameAsync(
            fixture.DriverId,
            new { firstName = FirstName, lastName = LastName });

        response.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var rows = await context.AuditEntries
            .AsNoTracking()
            .Where(entry => entry.ActorAccountId == fixture.DriverId)
            .ToListAsync(CancellationToken.None);

        Assert.NotEmpty(rows);

        foreach (var row in rows)
        {
            Assert.DoesNotContain(FirstName, row.Details ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain(LastName, row.Details ?? string.Empty, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Namn_ForsvinnerMedKontot()
    {
        // §KM.6: radering tar bort kontot och allt det ager, direkt och inte som en
        // markering. Namnet ligger pa kontoraden och foljer darfor med.
        var fixture = await SeedAsync("radering");

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var account = await context.Accounts
            .SingleAsync(a => a.Id == fixture.DriverId, CancellationToken.None);

        context.Accounts.Remove(account);
        await context.SaveChangesAsync(CancellationToken.None);

        var remaining = await context.Accounts
            .AsNoTracking()
            .AnyAsync(a => a.FirstName == FirstName && a.Id == fixture.DriverId, CancellationToken.None);

        Assert.False(remaining);
    }
}

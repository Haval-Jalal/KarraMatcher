using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KarraMatcher.Api.Features.Attendance;
using KarraMatcher.Application.Abstractions.Security;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Events;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Kallelsens flagga (`#56`, §KM.7, säkerhetschecklistan 2.7).
///
/// <para>
/// Funktionen levereras <b>avstängd</b>. Det som prövas här är att avstängd verkligen
/// betyder stängd på servern — inte bara att en knapp saknas i gränssnittet — och att
/// svaret är <c>404</c> och inte <c>403</c>, så att ingen kan kartlägga vilka lag som har
/// den påslagen genom att prova sig fram.
/// </para>
/// </summary>
public sealed class AttendanceFlagTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private static WebApplicationFactoryClientOptions ClientOptions => new() { HandleCookies = true };

    private sealed record Fixture(string Slug, Guid MatchId, Guid AdminId, Guid CoachId);

    private async Task<Fixture> SeedAsync(string suffix, bool enabled = false)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var now = DateTime.UtcNow;

        var club = new Club { Id = Guid.NewGuid(), Name = "Karra KIF", Slug = $"klubb-f-{suffix}" };
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
            Slug = $"gul-f-{suffix}",
            AttendanceEnabled = enabled,
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
            KickoffUtc = now.AddDays(3),
            OpponentName = "Torslanda",
            VenueId = venue.Id,
            IsHome = true,
            Status = EventStatus.Scheduled,
            IcsSequence = 0,
            UpdatedUtc = now,
        };

        var admin = new Account { Id = Guid.NewGuid(), Email = $"admin-f-{suffix}@example.com", CreatedUtc = now };
        var coach = new Account { Id = Guid.NewGuid(), Email = $"tranare-f-{suffix}@example.com", CreatedUtc = now };

        context.Clubs.Add(club);
        context.AgeGroups.Add(ageGroup);
        context.Teams.Add(team);
        context.Venues.Add(venue);
        context.Events.Add(match);
        context.Accounts.AddRange(admin, coach);

        await context.SaveChangesAsync(CancellationToken.None);

        return new Fixture(team.Slug, match.Id, admin.Id, coach.Id);
    }

    private string TokenFor(Guid accountId, bool isAdmin = false, params string[] coachOf)
    {
        using var scope = factory.Services.CreateScope();
        var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();

        return issuer.Issue(accountId, "konto@example.com", new AccountRoles(isAdmin, [], coachOf)).Token;
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

    /// <summary>Anropar prov-endpointen bakom grinden.</summary>
    private async Task<HttpResponseMessage> ProbeAsync(string path)
    {
        using var probe = factory.WithAttendanceProbe();
        using var client = probe.CreateClient();

        return await client.GetAsync(path, CancellationToken.None);
    }

    private async Task<HttpResponseMessage> SetFlagAsync(string slug, bool enabled, string token)
    {
        using var client = factory.CreateClient(ClientOptions);
        var (csrf, cookie) = await GetCsrfAsync(client, token);

        var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/v1/admin/teams/{slug}/attendance")
        {
            Content = JsonContent.Create(new { enabled }),
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        request.Headers.Add("Cookie", cookie);

        return await client.SendAsync(request, CancellationToken.None);
    }

    // ---- Grinden ar stangd nar flaggan ar av -----------------------------------------

    [Fact]
    public async Task AvslagenFlagga_GerFyrahundrafyra_ForEttLag()
    {
        var fixture = await SeedAsync("av-lag");

        var response = await ProbeAsync($"/test/attendance/team/{fixture.Slug}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AvslagenFlagga_GerFyrahundrafyra_ForEnMatch()
    {
        // Kallelsens adresser kommer att bara laget bade som slug och som match. Grinden
        // maste klara bada -- annars ar den halva vagen oppen.
        var fixture = await SeedAsync("av-match");

        var response = await ProbeAsync($"/test/attendance/match/{fixture.MatchId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AvslagenFlagga_GerAldrigFyrahundratre()
    {
        /*
         * Karnan i §KM.7. Ett 403 sager "funktionen finns, men inte for dig" -- och da gar
         * det att kartlagga vilka lag som har kallelsen paslagen genom att prova sig fram.
         */
        var fixture = await SeedAsync("aldrig-403");

        var response = await ProbeAsync($"/test/attendance/team/{fixture.Slug}");

        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OkantLag_GerFyrahundrafyra()
    {
        var response = await ProbeAsync("/test/attendance/team/finns-inte");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RouteUtanLag_ArStangd()
    {
        // En grind som inte vet vad den vaktar ska vara stangd, inte oppen. Det ar ett
        // programmeringsfel att satta attributet pa en route utan lag -- och da ska det
        // marka sig som ett stangt svar, inte som en tyst oppning.
        var response = await ProbeAsync("/test/attendance/utan-lag");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PaslagenFlagga_SlapperIgenom()
    {
        // Den omvanda kontrollen. En grind som stanger allt bevisar ingenting.
        var fixture = await SeedAsync("pa", enabled: true);

        var response = await ProbeAsync($"/test/attendance/team/{fixture.Slug}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---- Bara administrator far andra flaggan -----------------------------------------

    [Fact]
    public async Task Flaggan_UtanInloggning_Nekas()
    {
        var fixture = await SeedAsync("anon");

        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/v1/admin/teams/{fixture.Slug}/attendance",
            new { enabled = true },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Flaggan_SomTranare_Nekas()
    {
        /*
         * Att sla pa kallelsen ar att borja behandla uppgifter om barn pa servern. Det
         * beslutet hor till klubben och inte till en enskild tranare -- och det forutsatter
         * att samtyckesrutinen finns (§KM.6, #59).
         */
        var fixture = await SeedAsync("tranare");

        var response = await SetFlagAsync(
            fixture.Slug,
            enabled: true,
            TokenFor(fixture.CoachId, isAdmin: false, fixture.Slug));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Flaggan_SomAdmin_SlarPaOchAv()
    {
        var fixture = await SeedAsync("admin");
        var token = TokenFor(fixture.AdminId, isAdmin: true);

        var enable = await SetFlagAsync(fixture.Slug, enabled: true, token);

        Assert.Equal(HttpStatusCode.NoContent, enable.StatusCode);
        Assert.True(await FlagAsync(fixture.Slug));

        var disable = await SetFlagAsync(fixture.Slug, enabled: false, token);

        Assert.Equal(HttpStatusCode.NoContent, disable.StatusCode);
        Assert.False(await FlagAsync(fixture.Slug));
    }

    [Fact]
    public async Task Flaggan_ForOkantLag_GerFyrahundrafyra()
    {
        var fixture = await SeedAsync("okant");

        var response = await SetFlagAsync(
            "finns-inte",
            enabled: true,
            TokenFor(fixture.AdminId, isAdmin: true));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Flaggan_Andring_Auditloggas()
    {
        // §KM.10: att sla pa kallelsen ska ga att harleda i efterhand -- vem, vilket lag, nar.
        var fixture = await SeedAsync("audit");

        await SetFlagAsync(fixture.Slug, enabled: true, TokenFor(fixture.AdminId, isAdmin: true));

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var entry = await context.AuditEntries
            .AsNoTracking()
            .SingleOrDefaultAsync(
                e => e.ActorAccountId == fixture.AdminId && e.Action == "narvaro.paslagen",
                CancellationToken.None);

        Assert.NotNull(entry);
        Assert.Equal(fixture.Slug, entry.Details);
    }

    // ---- Vakten som faller bygget nar #57 glommer attributet --------------------------

    [Fact]
    public void NarvaroEndpoints_HarAllaGrinden()
    {
        /*
         * Kallelsens riktiga endpoints byggs i #57. Det har testet ar tomt i dag och det ar
         * meningen: den dag nagon lagger till en narvaro-route utan [RequireAttendanceEnabled]
         * faller bygget i stallet for att funktionen tyst blir tillganglig.
         *
         * Administratorns spak undantas -- den maste ga att na aven nar flaggan ar av, annars
         * gar den aldrig att sla pa.
         */
        var ungated = AttendanceRouteEndpoints()
            .Where(endpoint => !IsAdminLever(endpoint))
            .Where(endpoint => endpoint.Metadata.GetMetadata<RequireAttendanceEnabledAttribute>() is null)
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToArray();

        Assert.True(
            ungated.Length == 0,
            "Närvaro-endpoints saknar [RequireAttendanceEnabled] och är därmed öppna med "
                + "flaggan av (§KM.7): " + string.Join(", ", ungated));
    }

    private async Task<bool> FlagAsync(string slug)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        return await context.Teams
            .AsNoTracking()
            .Where(team => team.Slug == slug)
            .Select(team => team.AttendanceEnabled)
            .SingleAsync(CancellationToken.None);
    }

    private IEnumerable<RouteEndpoint> AttendanceRouteEndpoints() =>
        factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => (endpoint.RoutePattern.RawText ?? string.Empty)
                .Contains("attendance", StringComparison.OrdinalIgnoreCase));

    private static bool IsAdminLever(RouteEndpoint endpoint) =>
        (endpoint.RoutePattern.RawText ?? string.Empty)
            .StartsWith("api/v1/admin/", StringComparison.Ordinal);
}

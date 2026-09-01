using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using KarraMatcher.Application.Abstractions.Security;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Domain.Audit;
using KarraMatcher.Domain.Matches;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Tränarens matchhantering (§KM.4, §KM.10, checklistan 2.6 och 8.3).
///
/// <para>
/// Tre saker vaktas, och den första är den som gör verklig skada om den går sönder:
/// <b>en ändrad match måste öka <c>SEQUENCE</c></b>, annars uppdateras inte föräldrarnas
/// kalendrar. Appen visar då rätt tid medan telefonen påminner om den gamla — och ingen
/// märker det förrän någon står på fel plan.
/// </para>
/// </summary>
public sealed class MatchAdminTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private static readonly DateTime Kickoff = new(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);

    private static WebApplicationFactoryClientOptions ClientOptions => new() { HandleCookies = true };

    private sealed record Fixture(string Slug, Guid VenueId, Guid MatchId, Guid CoachAccountId);

    /// <summary>Två lag, en spelplats och en match i det första laget.</summary>
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
            IsHome = true,
            Status = MatchStatus.Scheduled,
            IcsSequence = 0,
            UpdatedUtc = Kickoff,
        };

        context.Clubs.Add(club);
        context.AgeGroups.Add(ageGroup);
        context.Teams.Add(team);
        context.Venues.Add(venue);
        context.Matches.Add(match);

        await context.SaveChangesAsync(CancellationToken.None);

        return new Fixture(team.Slug, venue.Id, match.Id, Guid.NewGuid());
    }

    private static string TokenFor(IServiceProvider services, Guid accountId, AccountRoles roles)
    {
        using var scope = services.CreateScope();
        var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();

        return issuer.Issue(accountId, "tranare@example.com", roles).Token;
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

    /// <summary>Ett anrop som tränare för <paramref name="coachOf"/>.</summary>
    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        string coachOf,
        Guid actorId,
        object? payload = null)
    {
        var token = TokenFor(factory.Services, actorId, new AccountRoles(false, [coachOf]));

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

    private static object Draft(DateTime kickoff, Guid venueId, string opponent = "Torslanda") =>
        new
        {
            kickoffUtc = kickoff,
            opponent,
            venueId,
            isHome = true,
            addressOverride = (string?)null,
            note = (string?)null,
        };

    // ---- Kalenderprenumerationerna maste fa veta --------------------------------------

    [Fact]
    public async Task Andring_OkarSekvensnumret()
    {
        /*
         * Utan det har uppdateras inte foraldrarnas kalendrar (§KM.4). Appen visar ratt
         * tid medan telefonen paminner om den gamla, och felet upptacks forst nar nagon
         * star pa fel plan vid fel tid.
         */
        var fixture = await SeedAsync("seq");

        var response = await SendAsync(
            HttpMethod.Put,
            $"/api/v1/teams/{fixture.Slug}/matches/{fixture.MatchId}",
            fixture.Slug,
            fixture.CoachAccountId,
            Draft(Kickoff.AddHours(2), fixture.VenueId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, await SequenceOf(fixture.MatchId));
    }

    [Fact]
    public async Task Instalining_OkarOcksaSekvensnumret()
    {
        // En inställd match som inte ökar sekvensen ligger kvar som "spelas" i kalendern.
        var fixture = await SeedAsync("cancel-seq");

        await SendAsync(
            HttpMethod.Post,
            $"/api/v1/teams/{fixture.Slug}/matches/{fixture.MatchId}/cancel",
            fixture.Slug,
            fixture.CoachAccountId);

        Assert.Equal(1, await SequenceOf(fixture.MatchId));
    }

    [Fact]
    public async Task Instalining_RaderarInteMatchen()
    {
        // §KM.4: kalenderposten ska bli kvar med STATUS:CANCELLED. Försvinner den helt
        // står matchen kvar i föräldrarnas kalendrar som om ingenting hänt.
        var fixture = await SeedAsync("cancel-keep");

        await SendAsync(
            HttpMethod.Post,
            $"/api/v1/teams/{fixture.Slug}/matches/{fixture.MatchId}/cancel",
            fixture.Slug,
            fixture.CoachAccountId);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        var match = await context.Matches.FindAsync([fixture.MatchId], CancellationToken.None);

        Assert.NotNull(match);
        Assert.Equal(MatchStatus.Cancelled, match.Status);
    }

    // ---- Tranaren rors bara sitt eget lag ---------------------------------------------

    [Fact]
    public async Task TranareForAnnatLag_Nekas()
    {
        var fixture = await SeedAsync("annat-lag");

        var response = await SendAsync(
            HttpMethod.Put,
            $"/api/v1/teams/{fixture.Slug}/matches/{fixture.MatchId}",
            "ett-helt-annat-lag",
            fixture.CoachAccountId,
            Draft(Kickoff, fixture.VenueId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MatchIEttAnnatLag_GerSammaSvarSomEnOkandMatch()
    {
        /*
         * Objektnivakontrollen (checklistan 2.6). Anroparen ar tranare for sitt lag och
         * skickar ett match-id som hor till ett annat -- policyn racker inte, eftersom den
         * bara sett pa lagets slug i adressen.
         *
         * Svaret ar 404 och inte 403, med flit: skulle de skilja sig kunde en tranare
         * rakna ut vilka match-id som finns i ett annat lag genom att prova sig fram.
         */
        var mine = await SeedAsync("mitt");
        var theirs = await SeedAsync("deras");

        var response = await SendAsync(
            HttpMethod.Put,
            $"/api/v1/teams/{mine.Slug}/matches/{theirs.MatchId}",
            mine.Slug,
            mine.CoachAccountId,
            Draft(Kickoff, mine.VenueId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UtanInloggning_Nekas()
    {
        var fixture = await SeedAsync("anonym");

        using var client = factory.CreateClient(ClientOptions);

        var response = await client.PostAsync(
            $"/api/v1/teams/{fixture.Slug}/matches", null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Validering ------------------------------------------------------------------

    [Fact]
    public async Task UtanMotstandare_Avvisas()
    {
        var fixture = await SeedAsync("validering");

        var response = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/teams/{fixture.Slug}/matches",
            fixture.Slug,
            fixture.CoachAccountId,
            Draft(Kickoff, fixture.VenueId, opponent: "  "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OkandSpelplats_Avvisas()
    {
        var fixture = await SeedAsync("spelplats");

        var response = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/teams/{fixture.Slug}/matches",
            fixture.Slug,
            fixture.CoachAccountId,
            Draft(Kickoff, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- Audit -----------------------------------------------------------------------

    [Fact]
    public async Task Andring_AuditLoggasMedForeOchEftervarde()
    {
        // "Vem flyttade matchen?" ska gå att besvara utan att gissa (§KM.10).
        var fixture = await SeedAsync("audit");

        await SendAsync(
            HttpMethod.Put,
            $"/api/v1/teams/{fixture.Slug}/matches/{fixture.MatchId}",
            fixture.Slug,
            fixture.CoachAccountId,
            Draft(Kickoff.AddHours(3), fixture.VenueId, opponent: "Kareby IS"));

        var entry = await AuditFor(fixture.MatchId, AuditActions.MatchUpdated);

        Assert.Equal(fixture.CoachAccountId, entry.ActorAccountId);
        Assert.Contains("->", entry.Details, StringComparison.Ordinal);
        Assert.Contains("Torslanda", entry.Details, StringComparison.Ordinal);
        Assert.Contains("Kareby IS", entry.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuditPosten_InnehallerAldrigNotisen()
    {
        /*
         * Notisen ar tranarens egna ord och raknas som potentiell PII (§KM.1). Att den
         * andrats far synas -- men inte vad den andrats till.
         *
         * Det ar precis den sortens falt som halkar in i en loggrad for att det "var
         * praktiskt att se hela diffen".
         */
        var fixture = await SeedAsync("notis");

        var payload = new
        {
            kickoffUtc = Kickoff,
            opponent = "Torslanda",
            venueId = fixture.VenueId,
            isHome = true,
            addressOverride = (string?)null,
            note = "Elias mamma kor, ring 070-1234567",
        };

        await SendAsync(
            HttpMethod.Put,
            $"/api/v1/teams/{fixture.Slug}/matches/{fixture.MatchId}",
            fixture.Slug,
            fixture.CoachAccountId,
            payload);

        var entry = await AuditFor(fixture.MatchId, AuditActions.MatchUpdated);

        Assert.DoesNotContain("Elias", entry.Details, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("070", entry.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Radering_AuditLoggasOchPostenOverlever()
    {
        var fixture = await SeedAsync("radering");

        var response = await SendAsync(
            HttpMethod.Delete,
            $"/api/v1/teams/{fixture.Slug}/matches/{fixture.MatchId}",
            fixture.Slug,
            fixture.CoachAccountId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var entry = await AuditFor(fixture.MatchId, AuditActions.MatchDeleted);

        Assert.Equal(fixture.MatchId, entry.SubjectId);
    }

    // ---- Hjalpare --------------------------------------------------------------------

    private async Task<int> SequenceOf(Guid matchId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var match = await context.Matches
            .AsNoTracking()
            .SingleAsync(m => m.Id == matchId, CancellationToken.None);

        return match.IcsSequence;
    }

    private async Task<AuditEntry> AuditFor(Guid subjectId, string action)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        return await context.AuditEntries
            .AsNoTracking()
            .SingleAsync(e => e.SubjectId == subjectId && e.Action == action, CancellationToken.None);
    }
}

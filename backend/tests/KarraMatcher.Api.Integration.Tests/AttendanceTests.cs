using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using KarraMatcher.Application.Abstractions.Security;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Attendance;
using KarraMatcher.Domain.Children;
using KarraMatcher.Domain.Matches;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Kallelsen och närvarosvaren (`#57`, §KM.7, §KM.1).
///
/// <para>
/// Tränaren kallar; en inloggad vuxen svarar för sin familj med ett antal — aldrig ett barn.
/// Allt ligger bakom grinden: med flaggan av är funktionen <c>404</c>, som om den inte fanns.
/// </para>
/// </summary>
public sealed class AttendanceTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private static WebApplicationFactoryClientOptions ClientOptions => new() { HandleCookies = true };

    private sealed record Fixture(string Slug, Guid MatchId, Guid CoachId, Guid ParentId);

    private async Task<Fixture> SeedAsync(
        string suffix,
        bool enabled = true,
        int kickoffDays = 3)
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
        var match = new Match
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            KickoffUtc = now.AddDays(kickoffDays),
            OpponentName = "Torslanda",
            VenueId = venue.Id,
            IsHome = true,
            Status = MatchStatus.Scheduled,
            IcsSequence = 0,
            UpdatedUtc = now,
        };

        var coach = new Account { Id = Guid.NewGuid(), Email = $"tranare-n-{suffix}@example.com", CreatedUtc = now };
        var parent = new Account { Id = Guid.NewGuid(), Email = $"foralder-n-{suffix}@example.com", CreatedUtc = now };

        // Foraldern ar medlem av laget (v2, §KM.3): vardnadshavare till ett barn i det. Utan
        // medlemskap kan hen inte na narvaroendpointen. Tranaren nar sina endpoints via
        // CoachOfTeam-anspraket och behover ingen vardnadskoppling.
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
        context.Matches.Add(match);
        context.Accounts.AddRange(coach, parent);
        context.Children.Add(child);
        context.Guardianships.Add(new Guardianship
        {
            Id = Guid.NewGuid(),
            AccountId = parent.Id,
            ChildId = child.Id,
            GrantedUtc = now,
        });

        await context.SaveChangesAsync(CancellationToken.None);

        return new Fixture(team.Slug, match.Id, coach.Id, parent.Id);
    }

    private string TokenFor(Guid accountId, params string[] coachOf)
    {
        using var scope = factory.Services.CreateScope();
        var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();

        return issuer.Issue(accountId, "konto@example.com", new AccountRoles(false, [], coachOf)).Token;
    }

    private static async Task<(string Token, string Cookie)> CsrfAsync(HttpClient client, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/csrf");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        var cookie = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("karra_csrf", StringComparison.Ordinal))
            .Split(';')[0];

        return (body.GetProperty("token").GetString()!, cookie);
    }

    private async Task<HttpResponseMessage> CallAsync(string slug, Guid matchId, string token)
    {
        using var client = factory.CreateClient(ClientOptions);
        var (csrf, cookie) = await CsrfAsync(client, token);

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/teams/{slug}/matches/{matchId}/attendance/call");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        request.Headers.Add("Cookie", cookie);

        return await client.SendAsync(request, CancellationToken.None);
    }

    private async Task<HttpResponseMessage> SubmitAsync(
        Guid matchId,
        string token,
        string status,
        int count)
    {
        using var client = factory.CreateClient(ClientOptions);
        var (csrf, cookie) = await CsrfAsync(client, token);

        var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/v1/matches/{matchId}/attendance/response")
        {
            Content = JsonContent.Create(new { status, count }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        request.Headers.Add("Cookie", cookie);

        return await client.SendAsync(request, CancellationToken.None);
    }

    private async Task<HttpResponseMessage> StateAsync(Guid matchId, string token)
    {
        using var client = factory.CreateClient(ClientOptions);

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/matches/{matchId}/attendance");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await client.SendAsync(request, CancellationToken.None);
    }

    // ---- Grinden ---------------------------------------------------------------------

    [Fact]
    public async Task AvslagenFlagga_KallaGerFyrahundrafyra()
    {
        var fixture = await SeedAsync("gate-call", enabled: false);

        var response = await CallAsync(fixture.Slug, fixture.MatchId, TokenFor(fixture.CoachId, fixture.Slug));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AvslagenFlagga_LagetGerFyrahundrafyra()
    {
        var fixture = await SeedAsync("gate-state", enabled: false);

        var response = await StateAsync(fixture.MatchId, TokenFor(fixture.ParentId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Tranaren kallar -------------------------------------------------------------

    [Fact]
    public async Task Tranare_KanKalla_OchOmKallelseArIdempotent()
    {
        var fixture = await SeedAsync("call");
        var token = TokenFor(fixture.CoachId, fixture.Slug);

        var first = await CallAsync(fixture.Slug, fixture.MatchId, token);
        var second = await CallAsync(fixture.Slug, fixture.MatchId, token);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
    }

    [Fact]
    public async Task IckeTranare_KanInteKalla()
    {
        // En inloggad vuxen som inte ar tranare for laget nekas -- CoachOfTeam provas mot
        // slugen i adressen.
        var fixture = await SeedAsync("not-coach");

        var response = await CallAsync(fixture.Slug, fixture.MatchId, TokenFor(fixture.ParentId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Tranare_KanInteKallaEnMatchIAnnatLag()
    {
        // Slugen i adressen racker inte: att matchen faktiskt hor till laget provas i tjansten.
        // En frammande match under egen slug ger 404, inte en kallelse i fel lag.
        var fixture = await SeedAsync("wrong-team");

        var response = await CallAsync(fixture.Slug, Guid.NewGuid(), TokenFor(fixture.CoachId, fixture.Slug));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Foraldern svarar ------------------------------------------------------------

    [Fact]
    public async Task Foralder_SvararOchAndrarAndaTillAvspark()
    {
        var fixture = await SeedAsync("respond");
        await CallAsync(fixture.Slug, fixture.MatchId, TokenFor(fixture.CoachId, fixture.Slug));

        var parent = TokenFor(fixture.ParentId);

        var first = await SubmitAsync(fixture.MatchId, parent, "Coming", 2);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        var afterFirst = await ReadStateAsync(fixture.MatchId, parent);
        Assert.True(afterFirst.CallOpen);
        Assert.Equal("Coming", afterFirst.Status);
        Assert.Equal(2, afterFirst.Count);

        var changed = await SubmitAsync(fixture.MatchId, parent, "Maybe", 1);
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        var afterChange = await ReadStateAsync(fixture.MatchId, parent);
        Assert.Equal("Maybe", afterChange.Status);
        Assert.Equal(1, afterChange.Count);
    }

    [Fact]
    public async Task Svar_UtanKallelse_GerConflict()
    {
        // Tranaren har inte kallat an -- ett svar hor ingenstans.
        var fixture = await SeedAsync("no-call");

        var response = await SubmitAsync(fixture.MatchId, TokenFor(fixture.ParentId), "Coming", 1);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Svar_EfterAvspark_GerConflict()
    {
        // Matchen har redan borjat -- ett svar sager ingenting langre.
        var fixture = await SeedAsync("closed", kickoffDays: -1);
        await CallAsync(fixture.Slug, fixture.MatchId, TokenFor(fixture.CoachId, fixture.Slug));

        var response = await SubmitAsync(fixture.MatchId, TokenFor(fixture.ParentId), "Coming", 1);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Svar_KommerUtanAntal_GerBadRequest()
    {
        var fixture = await SeedAsync("no-count");
        await CallAsync(fixture.Slug, fixture.MatchId, TokenFor(fixture.CoachId, fixture.Slug));

        var response = await SubmitAsync(fixture.MatchId, TokenFor(fixture.ParentId), "Coming", 0);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task KanInte_TvingasTillNoll()
    {
        // "Kan inte" tvingas till noll server-side, oavsett vad som skickas.
        var fixture = await SeedAsync("cant");
        await CallAsync(fixture.Slug, fixture.MatchId, TokenFor(fixture.CoachId, fixture.Slug));

        var parent = TokenFor(fixture.ParentId);
        var submit = await SubmitAsync(fixture.MatchId, parent, "CantCome", 3);
        Assert.Equal(HttpStatusCode.NoContent, submit.StatusCode);

        var state = await ReadStateAsync(fixture.MatchId, parent);
        Assert.Equal("CantCome", state.Status);
        Assert.Equal(0, state.Count);
    }

    [Fact]
    public async Task Laget_BarIngenBarnuppgift()
    {
        // §KM.1: svaret ar en status och ett antal. Ingen namn- eller barnuppgift far finnas
        // i svaret till klienten.
        var fixture = await SeedAsync("no-child");
        await CallAsync(fixture.Slug, fixture.MatchId, TokenFor(fixture.CoachId, fixture.Slug));

        var parent = TokenFor(fixture.ParentId);
        await SubmitAsync(fixture.MatchId, parent, "Coming", 2);

        var response = await StateAsync(fixture.MatchId, parent);
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        foreach (var forbidden in new[] { "name", "namn", "child", "barn", "player", "spelare" })
        {
            Assert.DoesNotContain(forbidden, body, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ---- Tranarens summering (#58) ---------------------------------------------------

    private async Task<HttpResponseMessage> SummaryAsync(string slug, Guid matchId, string token)
    {
        using var client = factory.CreateClient(ClientOptions);

        var request = new HttpRequestMessage(
          HttpMethod.Get,
          $"/api/v1/teams/{slug}/matches/{matchId}/attendance/summary");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await client.SendAsync(request, CancellationToken.None);
    }

    private async Task SeedResponsesAsync(
      Guid matchId,
      (string Name, AttendanceStatus Status, int Count)[] responders)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        var now = DateTime.UtcNow;

        context.AttendanceCalls.Add(new AttendanceCall
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            OpenedByAccountId = Guid.NewGuid(),
            OpenedUtc = now,
        });

        foreach (var (name, status, count) in responders)
        {
            var account = new Account
            {
                Id = Guid.NewGuid(),
                Email = $"{name.ToLowerInvariant()}-{Guid.NewGuid():N}@example.com",
                FirstName = name,
                CreatedUtc = now,
            };
            context.Accounts.Add(account);
            context.AttendanceResponses.Add(new AttendanceResponse
            {
                Id = Guid.NewGuid(),
                MatchId = matchId,
                AccountId = account.Id,
                Status = status,
                Count = count,
                CreatedUtc = now,
                UpdatedUtc = now,
            });
        }

        await context.SaveChangesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Tranare_SerSummeringMedAntalOchNamn()
    {
        var fixture = await SeedAsync("summary");
        await SeedResponsesAsync(
          fixture.MatchId,
          [
            ("Anna", AttendanceStatus.Coming, 2),
        ("Bengt", AttendanceStatus.CantCome, 0),
        ("Cecilia", AttendanceStatus.Maybe, 1),
          ]);

        var response = await SummaryAsync(fixture.Slug, fixture.MatchId, TokenFor(fixture.CoachId, fixture.Slug));
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        Assert.Equal(2, body.GetProperty("comingPeople").GetInt32());
        Assert.Equal(1, body.GetProperty("maybePeople").GetInt32());
        Assert.Equal(1, body.GetProperty("cantComeFamilies").GetInt32());
        Assert.Equal(3, body.GetProperty("respondedFamilies").GetInt32());

        var raw = await response.Content.ReadAsStringAsync(CancellationToken.None);
        Assert.Contains("Anna", raw, StringComparison.Ordinal);
        Assert.Contains("Cecilia", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Summering_SomIckeTranare_Nekas()
    {
        var fixture = await SeedAsync("summary-parent");

        var response = await SummaryAsync(fixture.Slug, fixture.MatchId, TokenFor(fixture.ParentId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Summering_ForMatchIAnnatLag_GerFyrahundrafyra()
    {
        var fixture = await SeedAsync("summary-wrong-team");

        var response = await SummaryAsync(fixture.Slug, Guid.NewGuid(), TokenFor(fixture.CoachId, fixture.Slug));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AvslagenFlagga_SummeringGerFyrahundrafyra()
    {
        var fixture = await SeedAsync("summary-gate", enabled: false);

        var response = await SummaryAsync(fixture.Slug, fixture.MatchId, TokenFor(fixture.CoachId, fixture.Slug));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record ResponseView(string Status, int Count);

    private sealed record StateView(bool CallOpen, ResponseView? MyResponse)
    {
        public string? Status => MyResponse?.Status;

        public int? Count => MyResponse?.Count;
    }

    private async Task<StateView> ReadStateAsync(Guid matchId, string token)
    {
        var response = await StateAsync(matchId, token);
        response.EnsureSuccessStatusCode();

        var element = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        var callOpen = element.GetProperty("callOpen").GetBoolean();

        if (element.TryGetProperty("myResponse", out var mine)
            && mine.ValueKind == JsonValueKind.Object)
        {
            return new StateView(
                callOpen,
                new ResponseView(mine.GetProperty("status").GetString()!, mine.GetProperty("count").GetInt32()));
        }

        return new StateView(callOpen, null);
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using KarraMatcher.Application.Abstractions.Security;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Audit;
using KarraMatcher.Domain.Children;
using KarraMatcher.Domain.Events;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Den riktade kallelsen per barn (§KM.7, §KM.1, `#199`).
///
/// <para>
/// En admin för truppen kallar utvalda barn — händelsens färg-lag plus vid behov barn ur de
/// andra lagen. Varje vårdnadshavare svarar Ja/Nej per eget barn. Barn visas som "Liam J".
/// Allt ligger bakom flaggan: med den av är funktionen 404 för vårdnadshavaren.
/// </para>
/// </summary>
public sealed class AttendanceTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private static WebApplicationFactoryClientOptions ClientOptions => new() { HandleCookies = true };

    private sealed record Fixture(
        Guid TruppId,
        Guid EventId,
        Guid AdminId,
        Guid SvartChild,
        Guid SvartGuardian,
        Guid SvartChild2,
        Guid SvartGuardian2,
        Guid GulChild,
        Guid GulGuardian);

    private async Task<Fixture> SeedAsync(
        string suffix,
        bool enabled = true,
        int kickoffDays = 3,
        EventType eventType = EventType.Match)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        var now = DateTime.UtcNow;

        var club = new Club { Id = Guid.NewGuid(), Name = "Karra KIF", Slug = $"klubb-n-{suffix}" };
        var trupp = new AgeGroup { Id = Guid.NewGuid(), ClubId = club.Id, Name = "P2016", Season = "2026" };
        var svart = new Team
        {
            Id = Guid.NewGuid(),
            AgeGroupId = trupp.Id,
            Name = "Svart",
            ColorHex = "#161616",
            Slug = $"svart-n-{suffix}",
            AttendanceEnabled = enabled,
        };
        var gul = new Team
        {
            Id = Guid.NewGuid(),
            AgeGroupId = trupp.Id,
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
        var isMatch = eventType == EventType.Match;
        var match = new Event
        {
            Id = Guid.NewGuid(),
            TeamId = svart.Id,
            Type = eventType,
            KickoffUtc = now.AddDays(kickoffDays),
            OpponentName = isMatch ? "Torslanda" : null,
            Title = isMatch ? null : "Lagträning",
            VenueId = venue.Id,
            IsHome = isMatch ? true : null,
            Status = EventStatus.Scheduled,
            UpdatedUtc = now,
        };
        var admin = new Account { Id = Guid.NewGuid(), Email = $"admin-n-{suffix}@example.com", CreatedUtc = now };

        context.Clubs.Add(club);
        context.AgeGroups.Add(trupp);
        context.Teams.AddRange(svart, gul);
        context.Venues.Add(venue);
        context.Events.Add(match);
        context.Accounts.Add(admin);
        await context.SaveChangesAsync(CancellationToken.None);

        var (svartChild, svartGuardian) = await SeedChildAsync(suffix, "s1", trupp.Id, svart.Id);
        var (svartChild2, svartGuardian2) = await SeedChildAsync(suffix, "s2", trupp.Id, svart.Id);
        var (gulChild, gulGuardian) = await SeedChildAsync(suffix, "g1", trupp.Id, gul.Id);

        return new Fixture(
            trupp.Id, match.Id, admin.Id,
            svartChild, svartGuardian, svartChild2, svartGuardian2, gulChild, gulGuardian);
    }

    private async Task<(Guid ChildId, Guid GuardianId)> SeedChildAsync(
        string suffix, string tag, Guid truppId, Guid teamId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        var now = DateTime.UtcNow;

        var guardian = new Account
        {
            Id = Guid.NewGuid(),
            Email = $"vh-{tag}-n-{suffix}@example.com",
            CreatedUtc = now,
        };
        var child = new Child
        {
            Id = Guid.NewGuid(),
            FirstName = "Liam",
            LastInitial = "J",
            AgeGroupId = truppId,
            TeamId = teamId,
            CreatedUtc = now,
        };

        context.Accounts.Add(guardian);
        context.Children.Add(child);
        context.Guardianships.Add(new Guardianship
        {
            Id = Guid.NewGuid(),
            AccountId = guardian.Id,
            ChildId = child.Id,
            GrantedUtc = now,
        });
        await context.SaveChangesAsync(CancellationToken.None);

        return (child.Id, guardian.Id);
    }

    private string AdminToken(Guid truppId) =>
        Token(Guid.NewGuid(), new AccountRoles(false, [truppId.ToString()], []));

    private string PlainToken(Guid accountId) =>
        Token(accountId, AccountRoles.None);

    private string Token(Guid accountId, AccountRoles roles)
    {
        using var scope = factory.Services.CreateScope();
        var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();

        return issuer.Issue(accountId, "konto@example.com", roles).Token;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, string token, object? payload = null)
    {
        using var client = factory.CreateClient(ClientOptions);
        var (csrf, cookie) = await CsrfAsync(client, token);

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

    private async Task<HttpResponseMessage> GetAsync(string path, string token)
    {
        using var client = factory.CreateClient(ClientOptions);
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request, CancellationToken.None);
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

    private static string AdminBase(Fixture f) =>
        $"/api/v1/admin/trupper/{f.TruppId}/events/{f.EventId}/kallelse";

    private Task<HttpResponseMessage> SetKallelseAsync(Fixture f, params Guid[] childIds) =>
        SendAsync(HttpMethod.Put, AdminBase(f), AdminToken(f.TruppId), new { childIds });

    private Task<HttpResponseMessage> RespondAsync(Guid eventId, Guid childId, Guid guardianId, string reply) =>
        SendAsync(
            HttpMethod.Put,
            $"/api/v1/events/{eventId}/kallelse/children/{childId}",
            PlainToken(guardianId),
            new { reply });

    private async Task<JsonElement> SummaryAsync(Fixture f)
    {
        var response = await GetAsync(AdminBase(f), AdminToken(f.TruppId));
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None)).Clone();
    }

    // ---- Admin skickar kallelse ------------------------------------------------------

    [Fact]
    public async Task Admin_KallarSvartPlusGulFillIn_OchAuditloggas()
    {
        var f = await SeedAsync("set");

        var set = await SetKallelseAsync(f, f.SvartChild, f.SvartChild2, f.GulChild);
        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);

        await AssertAuditedAsync(AuditActions.AttendanceCallOpened, f.EventId);

        var summary = await SummaryAsync(f);
        Assert.True(summary.GetProperty("callOpen").GetBoolean());
        Assert.Equal(3, summary.GetProperty("children").GetArrayLength());
        Assert.Equal(3, summary.GetProperty("notAnswered").GetInt32());
    }

    [Fact]
    public async Task Kalla_MedBarnUtanforTruppen_Ger400()
    {
        var f = await SeedAsync("outside");

        var response = await SetKallelseAsync(f, f.SvartChild, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Kalla_ForEventIAnnanTrupp_Ger404()
    {
        var f = await SeedAsync("idor");
        var other = await SeedAsync("idor-other");

        // Admin for sin egen trupp, men eventet hor till en annan trupp -> 404 (EventNotInTrupp).
        var response = await SendAsync(
            HttpMethod.Put,
            $"/api/v1/admin/trupper/{f.TruppId}/events/{other.EventId}/kallelse",
            AdminToken(f.TruppId),
            new { childIds = new[] { f.SvartChild } });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Vardnadshavaren svarar ------------------------------------------------------

    [Fact]
    public async Task GulFillIn_Vardnadshavare_KanSeOchSvara()
    {
        var f = await SeedAsync("fillin");
        await SetKallelseAsync(f, f.SvartChild, f.GulChild);

        // Gul-fill-in-barnets vardnadshavare ar INTE medlem av Svart, men ser sitt eget barn.
        var mine = await GetAsync($"/api/v1/events/{f.EventId}/kallelse", PlainToken(f.GulGuardian));
        mine.EnsureSuccessStatusCode();
        var body = await mine.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        Assert.True(body.GetProperty("callOpen").GetBoolean());
        var children = body.GetProperty("children").EnumerateArray().ToArray();
        Assert.Single(children);
        Assert.Equal(f.GulChild, children[0].GetProperty("childId").GetGuid());
        Assert.Equal("Liam J", children[0].GetProperty("displayName").GetString());

        var answer = await RespondAsync(f.EventId, f.GulChild, f.GulGuardian, "Coming");
        Assert.Equal(HttpStatusCode.NoContent, answer.StatusCode);
    }

    [Fact]
    public async Task IckeVardnadshavare_KanInteSvara_Ger404()
    {
        var f = await SeedAsync("not-guardian");
        await SetKallelseAsync(f, f.SvartChild);

        // GulGuardian ar inte vardnadshavare for SvartChild.
        var response = await RespondAsync(f.EventId, f.SvartChild, f.GulGuardian, "Coming");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Svar_JaOchNej_SynsISummeringen()
    {
        var f = await SeedAsync("summary");
        await SetKallelseAsync(f, f.SvartChild, f.SvartChild2, f.GulChild);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await RespondAsync(f.EventId, f.SvartChild, f.SvartGuardian, "Coming")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await RespondAsync(f.EventId, f.SvartChild2, f.SvartGuardian2, "NotComing")).StatusCode);

        var summary = await SummaryAsync(f);
        Assert.Equal(1, summary.GetProperty("coming").GetInt32());
        Assert.Equal(1, summary.GetProperty("notComing").GetInt32());
        Assert.Equal(1, summary.GetProperty("notAnswered").GetInt32());
    }

    [Fact]
    public async Task Svar_KanAndras()
    {
        var f = await SeedAsync("change");
        await SetKallelseAsync(f, f.SvartChild);

        await RespondAsync(f.EventId, f.SvartChild, f.SvartGuardian, "Coming");
        await RespondAsync(f.EventId, f.SvartChild, f.SvartGuardian, "NotComing");

        var summary = await SummaryAsync(f);
        Assert.Equal(0, summary.GetProperty("coming").GetInt32());
        Assert.Equal(1, summary.GetProperty("notComing").GetInt32());
    }

    [Fact]
    public async Task Svar_EfterAvspark_Ger409()
    {
        var f = await SeedAsync("closed", kickoffDays: -1);
        await SetKallelseAsync(f, f.SvartChild);

        var response = await RespondAsync(f.EventId, f.SvartChild, f.SvartGuardian, "Coming");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Svar_UtanKallelse_Ger404()
    {
        // Ingen kallelse har oppnats for barnet -> ett svar hor ingenstans (samlas till 404).
        var f = await SeedAsync("no-call");

        var response = await RespondAsync(f.EventId, f.SvartChild, f.SvartGuardian, "Coming");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Grinden ---------------------------------------------------------------------

    [Fact]
    public async Task AvslagenFlagga_VardnadshavareGet_Ger404()
    {
        var f = await SeedAsync("gate-mine", enabled: false);

        var response = await GetAsync($"/api/v1/events/{f.EventId}/kallelse", PlainToken(f.SvartGuardian));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AvslagenFlagga_AdminSet_Ger409()
    {
        var f = await SeedAsync("gate-set", enabled: false);

        var response = await SetKallelseAsync(f, f.SvartChild);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ---- Kallelsen galler bara match och traning (§KM.7, #289) -----------------------

    [Fact]
    public async Task Kalla_ForOvrigHandelse_Ger409()
    {
        // En ovrig handelse (cup, lagfest) har ingen kallelse. FE doljer knappen; servern ar
        // den riktiga grinden -- en direkt API-forfragan ska nekas, inte slappa igenom.
        var f = await SeedAsync("ovrig", eventType: EventType.Other);

        var response = await SetKallelseAsync(f, f.SvartChild);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Kalla_ForTraning_Tillats()
    {
        // Gransen ar Other-only, inte match-only: en traning kallar hela truppen ihop och
        // ska slappas igenom precis som en match.
        var f = await SeedAsync("traning", eventType: EventType.Training);

        var response = await SetKallelseAsync(f, f.SvartChild);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private async Task AssertAuditedAsync(string action, Guid subjectId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var found = await context.AuditEntries.AsNoTracking()
            .AnyAsync(e => e.Action == action && e.SubjectId == subjectId, CancellationToken.None);

        Assert.True(found, $"Ingen audit-rad '{action}' för {subjectId}.");
    }
}

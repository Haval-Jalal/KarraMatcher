using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using KarraMatcher.Application.Abstractions.Security;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Children;
using KarraMatcher.Domain.Events;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Cupens öppna anmälan (`#295`, §KM.12-stil). Tränaren sätter ett platstak; vilken
/// vårdnadshavare som helst i truppen anmäler sina barn först till kvarn tills platserna är slut.
/// Vaktar: taket tvingas server-side (full → 409), trupp-vid (förälder i annat lag kan anmäla),
/// att dra tillbaka frigör en plats, och att man bara når sitt eget barn.
/// </summary>
public sealed class CupSignupTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private static WebApplicationFactoryClientOptions ClientOptions => new() { HandleCookies = true };

    private sealed record Fixture(
        Guid TruppId,
        Guid CupId,
        Guid AdminId,
        Guid SvartChild,
        Guid SvartGuardian,
        Guid GulChild,
        Guid GulGuardian,
        Guid NonMemberId);

    private async Task<Fixture> SeedAsync(string suffix)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        var now = DateTime.UtcNow;

        var club = new Club { Id = Guid.NewGuid(), Name = "Kärra", Slug = $"klubb-cup-{suffix}" };
        var trupp = new AgeGroup { Id = Guid.NewGuid(), ClubId = club.Id, Name = "P2016", Season = "2026" };
        var svart = new Team
        {
            Id = Guid.NewGuid(),
            AgeGroupId = trupp.Id,
            Name = "Svart",
            ColorHex = "#161616",
            Slug = $"svart-cup-{suffix}",
        };
        var gul = new Team
        {
            Id = Guid.NewGuid(),
            AgeGroupId = trupp.Id,
            Name = "Gul",
            ColorHex = "#D9A21B",
            Slug = $"gul-cup-{suffix}",
        };
        var cup = new Event
        {
            Id = Guid.NewGuid(),
            TeamId = svart.Id,
            Type = EventType.Cup,
            KickoffUtc = now.AddDays(7),
            Title = "Sommarcup",
            Status = EventStatus.Scheduled,
            UpdatedUtc = now,
        };
        var admin = new Account { Id = Guid.NewGuid(), Email = $"admin-cup-{suffix}@example.com", CreatedUtc = now };

        context.Clubs.Add(club);
        context.AgeGroups.Add(trupp);
        context.Teams.AddRange(svart, gul);
        context.Events.Add(cup);
        context.Accounts.Add(admin);

        // Admin för truppen (DB-roll, så summeringens medlemskapskoll släpper in hen).
        context.TeamRoles.Add(new TeamRole
        {
            Id = Guid.NewGuid(),
            AccountId = admin.Id,
            AgeGroupId = trupp.Id,
            Role = RoleKind.Admin,
            GrantedUtc = now,
        });

        var nonMember = new Account { Id = Guid.NewGuid(), Email = $"utom-cup-{suffix}@example.com", CreatedUtc = now };
        context.Accounts.Add(nonMember);

        await context.SaveChangesAsync(CancellationToken.None);

        var (svartChild, svartGuardian) = await SeedChildAsync(suffix, "svart", trupp.Id, svart.Id);
        var (gulChild, gulGuardian) = await SeedChildAsync(suffix, "gul", trupp.Id, gul.Id);

        return new Fixture(
            trupp.Id, cup.Id, admin.Id, svartChild, svartGuardian, gulChild, gulGuardian, nonMember.Id);
    }

    private async Task<(Guid ChildId, Guid GuardianId)> SeedChildAsync(
        string suffix, string tag, Guid truppId, Guid teamId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        var now = DateTime.UtcNow;

        var guardian = new Account { Id = Guid.NewGuid(), Email = $"vh-{tag}-cup-{suffix}@example.com", CreatedUtc = now };
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

    private string GuardianToken(Guid guardianId) => Token(guardianId, AccountRoles.None);

    private Task<HttpResponseMessage> OpenCupAsync(Fixture f, int capacity) =>
        SendAsync(
            HttpMethod.Put,
            $"/api/v1/admin/trupper/{f.TruppId}/events/{f.CupId}/cup",
            AdminToken(f.TruppId),
            new { capacity });

    private Task<HttpResponseMessage> SignUpAsync(Fixture f, Guid childId, Guid guardianId) =>
        SendAsync(
            HttpMethod.Post,
            $"/api/v1/events/{f.CupId}/cup/children/{childId}",
            GuardianToken(guardianId));

    private Task<HttpResponseMessage> WithdrawAsync(Fixture f, Guid childId, Guid guardianId) =>
        SendAsync(
            HttpMethod.Delete,
            $"/api/v1/events/{f.CupId}/cup/children/{childId}",
            GuardianToken(guardianId));

    private async Task<JsonElement> SummaryAsync(Fixture f, Guid accountId)
    {
        var response = await GetAsync($"/api/v1/events/{f.CupId}/cup", GuardianToken(accountId));
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None)).Clone();
    }

    // ---- Trupp-vid cup-lista (#304) ---------------------------------------------------

    [Fact]
    public async Task TruppCups_ListarCupenMedAnmalningslage()
    {
        var f = await SeedAsync("lista");
        await OpenCupAsync(f, 3);
        await SignUpAsync(f, f.SvartChild, f.SvartGuardian);

        var response = await GetAsync($"/api/v1/trupper/{f.TruppId}/cups", GuardianToken(f.SvartGuardian));
        response.EnsureSuccessStatusCode();
        var cups = (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None))
            .EnumerateArray().ToArray();

        var cup = Assert.Single(cups);
        Assert.Equal(f.CupId, cup.GetProperty("eventId").GetGuid());
        Assert.Equal("Sommarcup", cup.GetProperty("title").GetString());
        Assert.Equal("Svart", cup.GetProperty("teamName").GetString());
        Assert.True(cup.GetProperty("open").GetBoolean());
        Assert.Equal(3, cup.GetProperty("capacity").GetInt32());
        Assert.Equal(2, cup.GetProperty("spotsLeft").GetInt32());
        Assert.False(cup.GetProperty("isFull").GetBoolean());
    }

    [Fact]
    public async Task TruppCups_ForIckeMedlem_Nekas()
    {
        var f = await SeedAsync("lista-nekas");
        await OpenCupAsync(f, 2);

        var response = await GetAsync($"/api/v1/trupper/{f.TruppId}/cups", GuardianToken(f.NonMemberId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- Öppna + anmäla ---------------------------------------------------------------

    [Fact]
    public async Task Tranare_Oppnar_OchForalderAnmaler_SynsISummeringen()
    {
        var f = await SeedAsync("open");

        Assert.Equal(HttpStatusCode.NoContent, (await OpenCupAsync(f, 2)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SignUpAsync(f, f.SvartChild, f.SvartGuardian)).StatusCode);

        var summary = await SummaryAsync(f, f.SvartGuardian);
        Assert.True(summary.GetProperty("open").GetBoolean());
        Assert.Equal(2, summary.GetProperty("capacity").GetInt32());
        Assert.Equal(1, summary.GetProperty("spotsTaken").GetInt32());
        Assert.Equal(1, summary.GetProperty("spotsLeft").GetInt32());
        Assert.False(summary.GetProperty("isFull").GetBoolean());
        Assert.Single(summary.GetProperty("signedUp").EnumerateArray());
        Assert.Equal("Liam J", summary.GetProperty("signedUp")[0].GetProperty("displayName").GetString());

        // Vårdnadshavaren ser sitt eget barn i "mine", nu markerat som anmält (`#296`).
        var mine = summary.GetProperty("mine").EnumerateArray().ToArray();
        Assert.Single(mine);
        Assert.Equal(f.SvartChild, mine[0].GetProperty("childId").GetGuid());
        Assert.True(mine[0].GetProperty("signedUp").GetBoolean());
    }

    // ---- Först till kvarn: full avvisas -----------------------------------------------

    [Fact]
    public async Task Full_Cup_AvvisarNastaAnmalan_Med409()
    {
        var f = await SeedAsync("full");
        await OpenCupAsync(f, 1);

        Assert.Equal(HttpStatusCode.NoContent, (await SignUpAsync(f, f.SvartChild, f.SvartGuardian)).StatusCode);

        // Andra barnet (annan förälder) möter en full cup — server-side, inte en dold knapp.
        var second = await SignUpAsync(f, f.GulChild, f.GulGuardian);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    // ---- Trupp-vid: förälder i ett annat färg-lag kan anmäla --------------------------

    [Fact]
    public async Task TruppVid_ForalderIAnnatLag_KanAnmala()
    {
        // Cupen ligger under Svart, men Gul-barnets förälder ska ändå kunna anmäla (trupp-vid).
        var f = await SeedAsync("truppvid");
        await OpenCupAsync(f, 5);

        var response = await SignUpAsync(f, f.GulChild, f.GulGuardian);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // ---- Dra tillbaka frigör en plats -------------------------------------------------

    [Fact]
    public async Task DraTillbaka_FrigorPlatsen()
    {
        var f = await SeedAsync("withdraw");
        await OpenCupAsync(f, 1);

        await SignUpAsync(f, f.SvartChild, f.SvartGuardian);

        // Full nu: Gul nekas.
        Assert.Equal(HttpStatusCode.Conflict, (await SignUpAsync(f, f.GulChild, f.GulGuardian)).StatusCode);

        // Svart drar tillbaka → platsen frigörs → Gul kommer in.
        Assert.Equal(HttpStatusCode.NoContent, (await WithdrawAsync(f, f.SvartChild, f.SvartGuardian)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SignUpAsync(f, f.GulChild, f.GulGuardian)).StatusCode);
    }

    // ---- Bara sitt eget barn ----------------------------------------------------------

    [Fact]
    public async Task Anmalan_ForAnnansBarn_Nekas()
    {
        var f = await SeedAsync("annans");
        await OpenCupAsync(f, 5);

        // Gul-föräldern försöker anmäla Svart-barnet (inte sitt) → 404 (avslöjar inget).
        var response = await SignUpAsync(f, f.SvartChild, f.GulGuardian);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Öppen anmälan gäller bara cup ------------------------------------------------

    [Fact]
    public async Task Oppna_ForEventSomInteArCup_Ger409()
    {
        var f = await SeedAsync("inte-cup");

        // Gör om cup-händelsen till en match och försök öppna öppen anmälan.
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
            var ev = await context.Events.FindAsync([f.CupId], CancellationToken.None);
            ev!.Type = EventType.Match;
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var response = await OpenCupAsync(f, 3);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ---- Anmälan innan tränaren öppnat ------------------------------------------------

    [Fact]
    public async Task Anmalan_InnanOppnad_Ger409()
    {
        var f = await SeedAsync("inte-oppen");

        var response = await SignUpAsync(f, f.SvartChild, f.SvartGuardian);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ---- Summeringen kräver trupp-medlemskap ------------------------------------------

    [Fact]
    public async Task Summering_ForIckeMedlem_Ger404()
    {
        var f = await SeedAsync("icke-medlem");
        await OpenCupAsync(f, 3);

        var response = await GetAsync($"/api/v1/events/{f.CupId}/cup", GuardianToken(f.NonMemberId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

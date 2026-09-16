using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Audit;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Ansökningar (§KM.3, `#194`): en förälder ansöker om att gå med i en trupp, admin ser kön
/// och godkänner/nekar, och en godkänd ansökan blir förälderns medlemskap (delad modell med
/// inbjudningarna, `#193`).
/// </summary>
public sealed class ApplicationTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private static WebApplicationFactoryClientOptions ClientOptions => new() { HandleCookies = true };

    // ---- Ansökningssidan (anonym) ----------------------------------------------------

    [Fact]
    public async Task ApplyInfo_ArAnonymOchVisarTruppen()
    {
        var trupp = await SeedTruppAsync("info");

        using var client = factory.CreateClient();
        var response = await client.GetAsync(
            $"/api/v1/trupper/{trupp.AgeGroupId}/apply-info", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        Assert.False(string.IsNullOrWhiteSpace(dto.GetProperty("truppName").GetString()));
    }

    [Fact]
    public async Task ApplyInfo_OkantTrupp_Ger404()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync(
            $"/api/v1/trupper/{Guid.NewGuid()}/apply-info", CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Ansöka ----------------------------------------------------------------------

    [Fact]
    public async Task Ansoka_UtanInloggning_Nekas()
    {
        var trupp = await SeedTruppAsync("anon");

        using var client = factory.CreateClient();
        var response = await client.PostAsync(
            $"/api/v1/trupper/{trupp.AgeGroupId}/applications", content: null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Ansoka_SomInloggad_Ger201_OchSynsIKon()
    {
        var trupp = await SeedTruppAsync("ansok");
        var accountId = await SeedAccountAsync("sokande@example.com");

        var apply = await SendAsync(
            HttpMethod.Post, $"/api/v1/trupper/{trupp.AgeGroupId}/applications",
            PlainToken("sokande@example.com", accountId));
        Assert.Equal(HttpStatusCode.Created, apply.StatusCode);

        var queue = await GetQueueAsync(trupp.AgeGroupId);
        Assert.Contains(
            queue.EnumerateArray(), a => a.GetProperty("applicantEmail").GetString() == "sokande@example.com");
    }

    [Fact]
    public async Task Ansoka_TvaGanger_Ger409()
    {
        var trupp = await SeedTruppAsync("dubbel");
        var accountId = await SeedAccountAsync("dubbel@example.com");

        await SendAsync(
            HttpMethod.Post, $"/api/v1/trupper/{trupp.AgeGroupId}/applications",
            PlainToken("dubbel@example.com", accountId));

        var again = await SendAsync(
            HttpMethod.Post, $"/api/v1/trupper/{trupp.AgeGroupId}/applications",
            PlainToken("dubbel@example.com", accountId));

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    // ---- Kön (behörighet) ------------------------------------------------------------

    [Fact]
    public async Task Kon_SomVanligMedlem_Nekas()
    {
        var trupp = await SeedTruppAsync("kon-medlem");

        var response = await SendAsync(
            HttpMethod.Get, $"/api/v1/admin/trupper/{trupp.AgeGroupId}/applications",
            PlainToken("ingen@example.com"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Kon_SomAdminForAnnanTrupp_Nekas()
    {
        var trupp = await SeedTruppAsync("kon-min");
        var annan = await SeedTruppAsync("kon-annans");

        var response = await SendAsync(
            HttpMethod.Get, $"/api/v1/admin/trupper/{trupp.AgeGroupId}/applications",
            AdminToken(annan.AgeGroupId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- Godkänna / neka -------------------------------------------------------------

    [Fact]
    public async Task Godkanna_GerMedlemskap_OchAuditloggas()
    {
        var trupp = await SeedTruppAsync("godkann");
        const string email = "godkann@example.com";
        var accountId = await SeedAccountAsync(email);

        await SendAsync(
            HttpMethod.Post, $"/api/v1/trupper/{trupp.AgeGroupId}/applications",
            PlainToken(email, accountId));
        var id = await FirstApplicationIdAsync(trupp.AgeGroupId);

        // Före godkännande: inte medlem.
        var before = await SendAsync(
            HttpMethod.Get, $"/api/v1/teams/{trupp.TeamSlug}/matches", PlainToken(email, accountId));
        Assert.Equal(HttpStatusCode.Forbidden, before.StatusCode);

        var approve = await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/trupper/{trupp.AgeGroupId}/applications/{id}/approve",
            AdminToken(trupp.AgeGroupId));
        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);
        await AssertAuditedAsync(AuditActions.ApplicationApproved, id);

        // Efter godkännande: medlem.
        var after = await SendAsync(
            HttpMethod.Get, $"/api/v1/teams/{trupp.TeamSlug}/matches", PlainToken(email, accountId));
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
    }

    [Fact]
    public async Task Neka_GerIngetMedlemskap_OchAuditloggas()
    {
        var trupp = await SeedTruppAsync("neka");
        const string email = "neka@example.com";
        var accountId = await SeedAccountAsync(email);

        await SendAsync(
            HttpMethod.Post, $"/api/v1/trupper/{trupp.AgeGroupId}/applications",
            PlainToken(email, accountId));
        var id = await FirstApplicationIdAsync(trupp.AgeGroupId);

        var deny = await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/trupper/{trupp.AgeGroupId}/applications/{id}/deny",
            AdminToken(trupp.AgeGroupId));
        Assert.Equal(HttpStatusCode.NoContent, deny.StatusCode);
        await AssertAuditedAsync(AuditActions.ApplicationDenied, id);

        var matches = await SendAsync(
            HttpMethod.Get, $"/api/v1/teams/{trupp.TeamSlug}/matches", PlainToken(email, accountId));
        Assert.Equal(HttpStatusCode.Forbidden, matches.StatusCode);
    }

    [Fact]
    public async Task Godkanna_RedanAvgjord_Ger409()
    {
        var trupp = await SeedTruppAsync("avgjord");
        var accountId = await SeedAccountAsync("avgjord@example.com");

        await SendAsync(
            HttpMethod.Post, $"/api/v1/trupper/{trupp.AgeGroupId}/applications",
            PlainToken("avgjord@example.com", accountId));
        var id = await FirstApplicationIdAsync(trupp.AgeGroupId);

        await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/trupper/{trupp.AgeGroupId}/applications/{id}/approve",
            AdminToken(trupp.AgeGroupId));

        var again = await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/trupper/{trupp.AgeGroupId}/applications/{id}/deny",
            AdminToken(trupp.AgeGroupId));

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    // ---- Hjälpare --------------------------------------------------------------------

    private sealed record Trupp(Guid AgeGroupId, string TeamSlug);

    private async Task<Trupp> SeedTruppAsync(string suffix)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var club = new Club { Id = Guid.NewGuid(), Name = "Kärra", Slug = $"klubb-app-{suffix}" };
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
            Slug = $"gul-app-{suffix}",
        };

        context.Clubs.Add(club);
        context.AgeGroups.Add(ageGroup);
        context.Teams.Add(team);
        await context.SaveChangesAsync(CancellationToken.None);

        return new Trupp(ageGroup.Id, team.Slug);
    }

    private async Task<Guid> SeedAccountAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Email = email.ToLowerInvariant(),
            CreatedUtc = DateTime.UtcNow,
        };

        context.Accounts.Add(account);
        await context.SaveChangesAsync(CancellationToken.None);

        return account.Id;
    }

    private async Task<JsonElement> GetQueueAsync(Guid ageGroupId)
    {
        var response = await SendAsync(
            HttpMethod.Get, $"/api/v1/admin/trupper/{ageGroupId}/applications", AdminToken(ageGroupId));
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None)).Clone();
    }

    private async Task<Guid> FirstApplicationIdAsync(Guid ageGroupId)
    {
        var queue = await GetQueueAsync(ageGroupId);
        return queue.EnumerateArray().First().GetProperty("id").GetGuid();
    }

    private string AdminToken(Guid ageGroupId) =>
        TestAuth.TokenFor(
            factory.Services, Guid.NewGuid(),
            new AccountRoles(false, [ageGroupId.ToString()], []), "admin@test");

    private string PlainToken(string email, Guid? accountId = null) =>
        TestAuth.TokenFor(factory.Services, accountId ?? Guid.NewGuid(), AccountRoles.None, email);

    private async Task AssertAuditedAsync(string action, Guid subjectId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var found = await context.AuditEntries.AsNoTracking()
            .AnyAsync(e => e.Action == action && e.SubjectId == subjectId, CancellationToken.None);

        Assert.True(found, $"Ingen audit-rad '{action}' för {subjectId}.");
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string token)
    {
        using var client = factory.CreateClient(ClientOptions);
        var (csrf, cookie) = await GetCsrfAsync(client, token);

        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        request.Headers.Add("Cookie", cookie);

        return await client.SendAsync(request, CancellationToken.None);
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
}

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
/// Tränartillsättning per lag (§KM.3, `#197`): admin tillsätter/avsätter tränare, ser dem per
/// lag, och når bara sin egen trupps lag (objektnivå-auktorisering).
/// </summary>
public sealed class CoachTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private static WebApplicationFactoryClientOptions ClientOptions => new() { HandleCookies = true };

    [Fact]
    public async Task Lista_SomVanligMedlem_Nekas()
    {
        var trupp = await SeedTruppAsync("kon");

        var response = await SendAsync(
            HttpMethod.Get, $"/api/v1/admin/trupper/{trupp.AgeGroupId}/coaches",
            PlainToken("ingen@example.com"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Tillsatt_Tranare_SynsIOversikten_OchAuditloggas()
    {
        var trupp = await SeedTruppAsync("tillsatt");
        var accountId = await SeedAccountAsync("tranare-tillsatt@example.com");

        var grant = await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/trupper/{trupp.AgeGroupId}/teams/{trupp.TeamId}/coaches",
            AdminToken(trupp.AgeGroupId), new { email = "tranare-tillsatt@example.com" });

        Assert.Equal(HttpStatusCode.Created, grant.StatusCode);
        var dto = await grant.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        Assert.Equal(accountId, dto.GetProperty("accountId").GetGuid());
        await AssertAuditedAsync(AuditActions.CoachGranted, accountId);

        var overview = await OverviewAsync(trupp.AgeGroupId);
        var team = overview.GetProperty("teams").EnumerateArray()
            .Single(t => t.GetProperty("teamId").GetGuid() == trupp.TeamId);
        Assert.Contains(
            team.GetProperty("coaches").EnumerateArray(),
            c => c.GetProperty("accountId").GetGuid() == accountId);
    }

    [Fact]
    public async Task Tillsatt_LagIAnnanTrupp_Ger404()
    {
        var trupp = await SeedTruppAsync("idor");
        var annan = await SeedTruppAsync("idor-annan");
        await SeedAccountAsync("tranare-idor@example.com");

        // Adminen för den andra truppen försöker tillsätta i den förstas lag via sin adress.
        var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/trupper/{annan.AgeGroupId}/teams/{trupp.TeamId}/coaches",
            AdminToken(annan.AgeGroupId), new { email = "tranare-idor@example.com" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Tillsatt_UtanKonto_Ger400()
    {
        var trupp = await SeedTruppAsync("utan-konto");

        var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/trupper/{trupp.AgeGroupId}/teams/{trupp.TeamId}/coaches",
            AdminToken(trupp.AgeGroupId), new { email = "finns-inte@example.com" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Tillsatt_Dubblett_Ger409()
    {
        var trupp = await SeedTruppAsync("dubbel");
        await SeedAccountAsync("tranare-dubbel@example.com");

        var first = await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/trupper/{trupp.AgeGroupId}/teams/{trupp.TeamId}/coaches",
            AdminToken(trupp.AgeGroupId), new { email = "tranare-dubbel@example.com" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var again = await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/trupper/{trupp.AgeGroupId}/teams/{trupp.TeamId}/coaches",
            AdminToken(trupp.AgeGroupId), new { email = "tranare-dubbel@example.com" });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Avsatt_Tranare_Ger204_OchAuditloggas()
    {
        var trupp = await SeedTruppAsync("avsatt");
        var accountId = await SeedAccountAsync("tranare-avsatt@example.com");

        await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/trupper/{trupp.AgeGroupId}/teams/{trupp.TeamId}/coaches",
            AdminToken(trupp.AgeGroupId), new { email = "tranare-avsatt@example.com" });

        var revoke = await SendAsync(
            HttpMethod.Delete,
            $"/api/v1/admin/trupper/{trupp.AgeGroupId}/teams/{trupp.TeamId}/coaches/{accountId}",
            AdminToken(trupp.AgeGroupId));

        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        await AssertAuditedAsync(AuditActions.CoachRevoked, accountId);

        var overview = await OverviewAsync(trupp.AgeGroupId);
        var team = overview.GetProperty("teams").EnumerateArray()
            .Single(t => t.GetProperty("teamId").GetGuid() == trupp.TeamId);
        Assert.Empty(team.GetProperty("coaches").EnumerateArray());
    }

    // ---- Hjälpare --------------------------------------------------------------------

    private sealed record Trupp(Guid AgeGroupId, Guid TeamId);

    private async Task<Trupp> SeedTruppAsync(string suffix)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var club = new Club { Id = Guid.NewGuid(), Name = "Kärra", Slug = $"klubb-c-{suffix}" };
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
            Slug = $"gul-c-{suffix}",
        };

        context.Clubs.Add(club);
        context.AgeGroups.Add(ageGroup);
        context.Teams.Add(team);
        await context.SaveChangesAsync(CancellationToken.None);

        return new Trupp(ageGroup.Id, team.Id);
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

    private async Task<JsonElement> OverviewAsync(Guid ageGroupId)
    {
        var response = await SendAsync(
            HttpMethod.Get, $"/api/v1/admin/trupper/{ageGroupId}/coaches", AdminToken(ageGroupId));
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None)).Clone();
    }

    private string AdminToken(Guid ageGroupId) =>
        TestAuth.TokenFor(
            factory.Services, Guid.NewGuid(),
            new AccountRoles(false, [ageGroupId.ToString()], []), "admin@test");

    private string PlainToken(string email) =>
        TestAuth.TokenFor(factory.Services, Guid.NewGuid(), AccountRoles.None, email);

    private async Task AssertAuditedAsync(string action, Guid subjectId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var found = await context.AuditEntries.AsNoTracking()
            .AnyAsync(e => e.Action == action && e.SubjectId == subjectId, CancellationToken.None);

        Assert.True(found, $"Ingen audit-rad '{action}' för {subjectId}.");
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, string token, object? payload = null)
    {
        using var client = factory.CreateClient(ClientOptions);
        var (csrf, cookie) = await GetCsrfAsync(client, token);

        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        request.Headers.Add("Cookie", cookie);

        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        return await client.SendAsync(request, CancellationToken.None);
    }

    private static async Task<(string Token, string Cookie)> GetCsrfAsync(
        HttpClient client, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/csrf");
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

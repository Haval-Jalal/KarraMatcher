using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Audit;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Superadmins plattformshantering (§KM.3, `#192`): skapa sport/klubb/trupp/lag och tillsätt
/// admins. Bara superadmin når hit; varje åtgärd audit-loggas.
/// </summary>
public sealed class AdministrationTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private static WebApplicationFactoryClientOptions ClientOptions => new() { HandleCookies = true };

    // ---- Grinden ---------------------------------------------------------------------

    [Fact]
    public async Task Sport_UtanInloggning_Nekas()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/sports", new { name = "Fotboll", slug = "fotboll" }, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Sport_SomIckeSuperadmin_Nekas()
    {
        // En vanlig inloggad (även en tränare) ska mötas av 403 — bara superadmin når hit.
        var response = await SendAsync(
            HttpMethod.Post, "/api/v1/admin/sports", superAdmin: false,
            new { name = "Fotboll", slug = "fotboll-icke" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- Sport -----------------------------------------------------------------------

    [Fact]
    public async Task Sport_SomSuperadmin_SkapasOchAuditloggas()
    {
        var response = await SendAsync(
            HttpMethod.Post, "/api/v1/admin/sports", superAdmin: true,
            new { name = "Innebandy", slug = "innebandy" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        var id = dto.GetProperty("id").GetGuid();

        Assert.Equal("Innebandy", dto.GetProperty("name").GetString());
        Assert.Equal("innebandy", dto.GetProperty("slug").GetString());

        await AssertAuditedAsync(AuditActions.SportCreated, id);
    }

    [Fact]
    public async Task Sport_MedUpptagenSlug_Ger409()
    {
        await SendAsync(
            HttpMethod.Post, "/api/v1/admin/sports", superAdmin: true,
            new { name = "Handboll", slug = "handboll" });

        var again = await SendAsync(
            HttpMethod.Post, "/api/v1/admin/sports", superAdmin: true,
            new { name = "Handboll 2", slug = "handboll" });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Med Mellanslag")]
    [InlineData("Åäö")]
    public async Task Sport_MedOgiltigSlug_Ger400(string slug)
    {
        var response = await SendAsync(
            HttpMethod.Post, "/api/v1/admin/sports", superAdmin: true,
            new { name = "Test", slug });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Sport_Andras_OchListas()
    {
        var id = await CreateSportAsync("bordtennis");

        var update = await SendAsync(
            HttpMethod.Put, $"/api/v1/admin/sports/{id}", superAdmin: true,
            new { name = "Bordtennis (ändrad)" });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var list = await SendAsync(HttpMethod.Get, "/api/v1/admin/sports", superAdmin: true);
        var sports = await list.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        Assert.Contains(
            sports.EnumerateArray(),
            s => s.GetProperty("id").GetGuid() == id
                && s.GetProperty("name").GetString() == "Bordtennis (ändrad)");
    }

    // ---- Trupp och lag (hierarkin) ---------------------------------------------------

    [Fact]
    public async Task Trupp_SkapasUnderKlubbOchSport_OchLagUnderTrupp()
    {
        var sportId = await CreateSportAsync("fotboll-hier");
        var clubId = await CreateClubAsync("karra-hier");

        var truppResponse = await SendAsync(
            HttpMethod.Post, "/api/v1/admin/trupper", superAdmin: true,
            new { clubId, sportId, name = "P2016", season = "2026" });

        Assert.Equal(HttpStatusCode.Created, truppResponse.StatusCode);
        var trupp = await truppResponse.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        var truppId = trupp.GetProperty("id").GetGuid();
        Assert.Equal("Kärra hier", trupp.GetProperty("clubName").GetString());

        var lagResponse = await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/trupper/{truppId}/lag", superAdmin: true,
            new { name = "Gul", colorHex = "#D9A21B", slug = "gul-hier" });

        Assert.Equal(HttpStatusCode.Created, lagResponse.StatusCode);
        await AssertAuditedAsync(AuditActions.LagCreated, lagResponse);

        var list = await SendAsync(
            HttpMethod.Get, $"/api/v1/admin/trupper/{truppId}/lag", superAdmin: true);
        var lag = await list.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        Assert.Single(lag.EnumerateArray());
    }

    [Fact]
    public async Task Trupp_MedDubblettNamn_Ger409()
    {
        var sportId = await CreateSportAsync("fotboll-dubb");
        var clubId = await CreateClubAsync("karra-dubb");

        await SendAsync(
            HttpMethod.Post, "/api/v1/admin/trupper", superAdmin: true,
            new { clubId, sportId, name = "P2015", season = "2026" });

        var again = await SendAsync(
            HttpMethod.Post, "/api/v1/admin/trupper", superAdmin: true,
            new { clubId, sportId, name = "P2015", season = "2026" });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Trupp_MedOkandKlubb_Ger400()
    {
        var sportId = await CreateSportAsync("fotboll-utanklubb");

        var response = await SendAsync(
            HttpMethod.Post, "/api/v1/admin/trupper", superAdmin: true,
            new { clubId = Guid.NewGuid(), sportId, name = "P2014", season = "2026" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Lag_MedUpptagenSlug_Ger409()
    {
        var truppId = await CreateTruppAsync("slug-krock");

        await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/trupper/{truppId}/lag", superAdmin: true,
            new { name = "Blå", colorHex = "#1B5FD9", slug = "delad-slug" });

        var again = await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/trupper/{truppId}/lag", superAdmin: true,
            new { name = "Vit", colorHex = "#D9D9D9", slug = "delad-slug" });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Lag_SkapasAvTruppensAdmin_Ger201()
    {
        // Poängen med #261: lag är truppens admins ansvar, inte bara superadmins.
        var truppId = await CreateTruppAsync("admin-lag");
        var adminRoles = new AccountRoles(false, [truppId.ToString()], []);

        var response = await SendWithRolesAsync(
            HttpMethod.Post, $"/api/v1/admin/trupper/{truppId}/lag", adminRoles, "admin@test",
            new { name = "Grön", colorHex = "#1B9E4B", slug = "gron-admin" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Lag_NekasForAdminAvAnnanTrupp_Ger403()
    {
        var truppId = await CreateTruppAsync("admin-fel-trupp");
        var annanTruppAdmin = new AccountRoles(false, [Guid.NewGuid().ToString()], []);

        var response = await SendWithRolesAsync(
            HttpMethod.Post, $"/api/v1/admin/trupper/{truppId}/lag", annanTruppAdmin, "fel@test",
            new { name = "Röd", colorHex = "#D91B1B", slug = "rod-fel" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- Admin-tilldelning -----------------------------------------------------------

    [Fact]
    public async Task Admin_TilldelasBefintligtKonto_DykerUppILista_OchAuditloggas()
    {
        var truppId = await CreateTruppAsync("admin-till");
        var (accountId, email) = await SeedAccountAsync("blivande-admin");

        var grant = await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/trupper/{truppId}/admins", superAdmin: true,
            new { email });

        Assert.Equal(HttpStatusCode.Created, grant.StatusCode);
        await AssertAuditedAsync(AuditActions.AdminGranted, accountId);

        var list = await SendAsync(
            HttpMethod.Get, $"/api/v1/admin/trupper/{truppId}/admins", superAdmin: true);
        var admins = await list.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        Assert.Contains(
            admins.EnumerateArray(), a => a.GetProperty("accountId").GetGuid() == accountId);
    }

    [Fact]
    public async Task Admin_ForOkandAdress_Ger400()
    {
        var truppId = await CreateTruppAsync("admin-okand");

        var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/trupper/{truppId}/admins", superAdmin: true,
            new { email = "finns-inte@example.com" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Admin_TilldelasTvaGanger_Ger409()
    {
        var truppId = await CreateTruppAsync("admin-dubbel");
        var (_, email) = await SeedAccountAsync("dubbel-admin");

        await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/trupper/{truppId}/admins", superAdmin: true, new { email });

        var again = await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/trupper/{truppId}/admins", superAdmin: true, new { email });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Admin_Aterkallas_TarBortRollen_OchAuditloggas()
    {
        var truppId = await CreateTruppAsync("admin-bort");
        var (accountId, email) = await SeedAccountAsync("bort-admin");

        await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/trupper/{truppId}/admins", superAdmin: true, new { email });

        var revoke = await SendAsync(
            HttpMethod.Delete, $"/api/v1/admin/trupper/{truppId}/admins/{accountId}", superAdmin: true);

        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        await AssertAuditedAsync(AuditActions.AdminRevoked, accountId);

        var list = await SendAsync(
            HttpMethod.Get, $"/api/v1/admin/trupper/{truppId}/admins", superAdmin: true);
        var admins = await list.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        Assert.Empty(admins.EnumerateArray());
    }

    // ---- Hjälpare --------------------------------------------------------------------

    private async Task<Guid> CreateSportAsync(string slug)
    {
        var response = await SendAsync(
            HttpMethod.Post, "/api/v1/admin/sports", superAdmin: true,
            new { name = slug, slug });
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        return dto.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateClubAsync(string slug)
    {
        var response = await SendAsync(
            HttpMethod.Post, "/api/v1/admin/clubs", superAdmin: true,
            new { name = "Kärra " + slug.Replace("karra-", "", StringComparison.Ordinal), slug });
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        return dto.GetProperty("id").GetGuid();
    }

    /// <summary>Skapar en sport, en klubb och en trupp och returnerar truppens id.</summary>
    private async Task<Guid> CreateTruppAsync(string suffix)
    {
        var sportId = await CreateSportAsync($"sport-{suffix}");
        var clubId = await CreateClubAsync($"karra-{suffix}");

        var response = await SendAsync(
            HttpMethod.Post, "/api/v1/admin/trupper", superAdmin: true,
            new { clubId, sportId, name = "P2016", season = "2026" });
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        return dto.GetProperty("id").GetGuid();
    }

    private async Task<(Guid AccountId, string Email)> SeedAccountAsync(string suffix)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var email = $"{suffix}@example.com";
        var account = new Account { Id = Guid.NewGuid(), Email = email, CreatedUtc = DateTime.UtcNow };

        context.Accounts.Add(account);
        await context.SaveChangesAsync(CancellationToken.None);

        return (account.Id, email);
    }

    private async Task AssertAuditedAsync(string action, Guid subjectId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var found = await context.AuditEntries.AsNoTracking()
            .AnyAsync(e => e.Action == action && e.SubjectId == subjectId, CancellationToken.None);

        Assert.True(found, $"Ingen audit-rad '{action}' för {subjectId}.");
    }

    private async Task AssertAuditedAsync(string action, HttpResponseMessage created)
    {
        var dto = await created.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        await AssertAuditedAsync(action, dto.GetProperty("id").GetGuid());
    }

    private Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, bool superAdmin, object? payload = null)
    {
        var roles = superAdmin ? new AccountRoles(true, [], []) : AccountRoles.None;
        return SendWithRolesAsync(
            method, path, roles, superAdmin ? "super@test" : "user@test", payload);
    }

    /// <summary>Skickar med en godtycklig rolluppsättning — för att pröva admin- och medlemsvägar.</summary>
    private async Task<HttpResponseMessage> SendWithRolesAsync(
        HttpMethod method, string path, AccountRoles roles, string email, object? payload = null)
    {
        var token = TestAuth.TokenFor(factory.Services, Guid.NewGuid(), roles, email);

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

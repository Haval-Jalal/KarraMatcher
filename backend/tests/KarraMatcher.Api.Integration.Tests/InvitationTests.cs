using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Audit;
using KarraMatcher.Domain.Invitations;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Inbjudningar (§KM.3, `#193`): admin bjuder in en vårdnadshavare, föräldern accepterar via
/// länk och blir medlem i truppen, token går ut och kan inte återanvändas.
/// </summary>
public sealed class InvitationTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private static WebApplicationFactoryClientOptions ClientOptions => new() { HandleCookies = true };

    // ---- Skapa (behörighet) ----------------------------------------------------------

    [Fact]
    public async Task Skapa_UtanInloggning_Nekas()
    {
        var trupp = await SeedTruppAsync("anon");

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/v1/admin/trupper/{trupp.AgeGroupId}/invitations",
            new { email = "foralder@example.com" }, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Skapa_SomVanligMedlem_Nekas()
    {
        var trupp = await SeedTruppAsync("medlem");

        var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/trupper/{trupp.AgeGroupId}/invitations",
            PlainToken("ingen@example.com"), new { email = "foralder@example.com" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Skapa_SomAdminForAnnanTrupp_Nekas()
    {
        var trupp = await SeedTruppAsync("min");
        var annan = await SeedTruppAsync("annans");

        var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/trupper/{trupp.AgeGroupId}/invitations",
            AdminToken(annan.AgeGroupId), new { email = "foralder@example.com" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Skapa_SomTruppadmin_Ger201_ListasOchAuditloggas()
    {
        var trupp = await SeedTruppAsync("skapa");

        var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/trupper/{trupp.AgeGroupId}/invitations",
            AdminToken(trupp.AgeGroupId), new { email = "Ny.Foralder@Example.com" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        var id = created.GetProperty("invitation").GetProperty("id").GetGuid();
        Assert.Equal("Pending", created.GetProperty("invitation").GetProperty("status").GetString());
        Assert.Contains("/inbjudan/", created.GetProperty("acceptUrl").GetString()!, StringComparison.Ordinal);

        await AssertAuditedAsync(AuditActions.InvitationCreated, id);

        var list = await SendAsync(
            HttpMethod.Get, $"/api/v1/admin/trupper/{trupp.AgeGroupId}/invitations",
            AdminToken(trupp.AgeGroupId));
        var pending = await list.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        Assert.Contains(pending.EnumerateArray(), i => i.GetProperty("id").GetGuid() == id);
    }

    // ---- Acceptera (huvudflödet) -----------------------------------------------------

    [Fact]
    public async Task Acceptera_SomRattAdress_GerMedlemskap()
    {
        var trupp = await SeedTruppAsync("accept");
        const string email = "parent-accept@example.com";
        var accountId = await SeedAccountAsync(email);

        var token = await CreateInvitationAsync(trupp.AgeGroupId, email);

        // Före accept: inte medlem — lagets schema nekas.
        var before = await SendAsync(
            HttpMethod.Get, $"/api/v1/teams/{trupp.TeamSlug}/events", PlainToken(email, accountId));
        Assert.Equal(HttpStatusCode.Forbidden, before.StatusCode);

        var accept = await SendAsync(
            HttpMethod.Post, $"/api/v1/invitations/{token}/accept", PlainToken(email, accountId));
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        await AssertAuditedAsync(AuditActions.InvitationAccepted, subjectByAccount: accountId);

        // Efter accept: medlem — schemat går att läsa.
        var after = await SendAsync(
            HttpMethod.Get, $"/api/v1/teams/{trupp.TeamSlug}/events", PlainToken(email, accountId));
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
    }

    [Fact]
    public async Task Acceptera_SomFelAdress_Ger403()
    {
        var trupp = await SeedTruppAsync("fel-adress");
        var token = await CreateInvitationAsync(trupp.AgeGroupId, "ratt@example.com");

        var otherId = await SeedAccountAsync("fel@example.com");

        var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/invitations/{token}/accept",
            PlainToken("fel@example.com", otherId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Acceptera_TvaGanger_Ger409()
    {
        var trupp = await SeedTruppAsync("reuse");
        const string email = "reuse@example.com";
        var accountId = await SeedAccountAsync(email);
        var token = await CreateInvitationAsync(trupp.AgeGroupId, email);

        var first = await SendAsync(
            HttpMethod.Post, $"/api/v1/invitations/{token}/accept", PlainToken(email, accountId));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await SendAsync(
            HttpMethod.Post, $"/api/v1/invitations/{token}/accept", PlainToken(email, accountId));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Acceptera_UtgangenLank_Ger410()
    {
        var trupp = await SeedTruppAsync("utgangen");
        const string email = "utgangen@example.com";
        var accountId = await SeedAccountAsync(email);

        // Seedad direkt med en utgången tidsstämpel.
        var token = await SeedInvitationAsync(
            trupp.AgeGroupId, email, DateTime.UtcNow.AddDays(-1), InvitationStatus.Pending);

        var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/invitations/{token}/accept", PlainToken(email, accountId));

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task Acceptera_UtanInloggning_Nekas()
    {
        var trupp = await SeedTruppAsync("accept-anon");
        var token = await CreateInvitationAsync(trupp.AgeGroupId, "x@example.com");

        using var client = factory.CreateClient();
        var response = await client.PostAsync(
            $"/api/v1/invitations/{token}/accept", content: null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Förhandsvisning (anonym) ----------------------------------------------------

    [Fact]
    public async Task Forhandsvisning_ArAnonymOchVisarTruppen()
    {
        var trupp = await SeedTruppAsync("preview");
        var token = await CreateInvitationAsync(trupp.AgeGroupId, "preview@example.com");

        using var client = factory.CreateClient();
        var response = await client.GetAsync($"/api/v1/invitations/{token}", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        Assert.True(dto.GetProperty("valid").GetBoolean());
        Assert.Equal("preview@example.com", dto.GetProperty("email").GetString());
        Assert.False(string.IsNullOrWhiteSpace(dto.GetProperty("truppName").GetString()));
    }

    [Fact]
    public async Task Forhandsvisning_OkantToken_Ger404()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync(
            "/api/v1/invitations/inte-en-riktig-token", CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Återkalla -------------------------------------------------------------------

    [Fact]
    public async Task Aterkalla_VantandeInbjudan_Ger204()
    {
        var trupp = await SeedTruppAsync("revoke");
        var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/trupper/{trupp.AgeGroupId}/invitations",
            AdminToken(trupp.AgeGroupId), new { email = "revoke@example.com" });
        var created = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        var id = created.GetProperty("invitation").GetProperty("id").GetGuid();

        var revoke = await SendAsync(
            HttpMethod.Delete, $"/api/v1/admin/trupper/{trupp.AgeGroupId}/invitations/{id}",
            AdminToken(trupp.AgeGroupId));

        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        var list = await SendAsync(
            HttpMethod.Get, $"/api/v1/admin/trupper/{trupp.AgeGroupId}/invitations",
            AdminToken(trupp.AgeGroupId));
        var pending = await list.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        Assert.DoesNotContain(pending.EnumerateArray(), i => i.GetProperty("id").GetGuid() == id);
    }

    // ---- Hjälpare --------------------------------------------------------------------

    private sealed record Trupp(Guid AgeGroupId, Guid TeamId, string TeamSlug);

    private async Task<Trupp> SeedTruppAsync(string suffix)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var club = new Club { Id = Guid.NewGuid(), Name = "Kärra", Slug = $"klubb-inv-{suffix}" };
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
            Slug = $"gul-inv-{suffix}",
        };

        context.Clubs.Add(club);
        context.AgeGroups.Add(ageGroup);
        context.Teams.Add(team);
        await context.SaveChangesAsync(CancellationToken.None);

        return new Trupp(ageGroup.Id, team.Id, team.Slug);
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

    /// <summary>Skapar en inbjudan via API:t och plockar ut token ur accept-länken.</summary>
    private async Task<string> CreateInvitationAsync(Guid ageGroupId, string email)
    {
        var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/trupper/{ageGroupId}/invitations",
            AdminToken(ageGroupId), new { email });
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        var acceptUrl = created.GetProperty("acceptUrl").GetString()!;

        return acceptUrl.Split("/inbjudan/", StringSplitOptions.None)[1];
    }

    /// <summary>Seedar en inbjudan direkt, för utgångs-/statusfall. Returnerar råtoken.</summary>
    private async Task<string> SeedInvitationAsync(
        Guid ageGroupId, string email, DateTime expiresUtc, InvitationStatus status)
    {
        var rawToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        context.Invitations.Add(new Invitation
        {
            Id = Guid.NewGuid(),
            AgeGroupId = ageGroupId,
            Email = email.ToLowerInvariant(),
            TokenHash = SessionIssuer.Hash(rawToken),
            CreatedByAccountId = Guid.NewGuid(),
            CreatedUtc = DateTime.UtcNow.AddDays(-2),
            ExpiresUtc = expiresUtc,
            Status = status,
        });
        await context.SaveChangesAsync(CancellationToken.None);

        return rawToken;
    }

    private string AdminToken(Guid ageGroupId) =>
        TestAuth.TokenFor(
            factory.Services, Guid.NewGuid(),
            new AccountRoles(false, [ageGroupId.ToString()], []), "admin@test");

    private string PlainToken(string email, Guid? accountId = null) =>
        TestAuth.TokenFor(
            factory.Services, accountId ?? Guid.NewGuid(), AccountRoles.None, email);

    private async Task AssertAuditedAsync(
        string action, Guid? subjectId = null, Guid? subjectByAccount = null)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var found = subjectId is not null
            ? await context.AuditEntries.AsNoTracking()
                .AnyAsync(e => e.Action == action && e.SubjectId == subjectId, CancellationToken.None)
            : await context.AuditEntries.AsNoTracking()
                .AnyAsync(e => e.Action == action && e.ActorAccountId == subjectByAccount,
                    CancellationToken.None);

        Assert.True(found, $"Ingen audit-rad '{action}'.");
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, string token, object? payload = null)
    {
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

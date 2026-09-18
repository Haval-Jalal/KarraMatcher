using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Application.Features.Consent;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Audit;
using KarraMatcher.Domain.Consent;
using KarraMatcher.Domain.Invitations;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Barnhantering (§KM.1/§KM.6, `#196`): skapa barn i en trupp, sortera i lag, koppla
/// vårdnadshavare (kräver samtycke), överblick per lag.
/// </summary>
public sealed class ChildTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private static WebApplicationFactoryClientOptions ClientOptions => new() { HandleCookies = true };

    // ---- Behörighet ------------------------------------------------------------------

    [Fact]
    public async Task Roster_SomVanligMedlem_Nekas()
    {
        var trupp = await SeedTruppAsync("kon");

        var response = await SendAsync(
            HttpMethod.Get, $"/api/v1/admin/trupper/{trupp.AgeGroupId}/children",
            PlainToken("ingen@example.com"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- Barn-CRUD + sortering -------------------------------------------------------

    [Fact]
    public async Task Skapa_Barn_ILag_SynsIRoster_OchAuditloggas()
    {
        var trupp = await SeedTruppAsync("skapa");

        var create = await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/trupper/{trupp.AgeGroupId}/children",
            AdminToken(trupp.AgeGroupId),
            new { firstName = "Liam", lastInitial = "J", teamId = trupp.TeamId });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var dto = await create.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        Assert.Equal("Liam J", dto.GetProperty("displayName").GetString());
        Assert.Equal(trupp.TeamId, dto.GetProperty("teamId").GetGuid());
        await AssertAuditedAsync(AuditActions.ChildCreated, dto.GetProperty("id").GetGuid());

        var roster = await RosterAsync(trupp.AgeGroupId);
        Assert.Contains(
            roster.GetProperty("children").EnumerateArray(),
            c => c.GetProperty("displayName").GetString() == "Liam J");
    }

    [Fact]
    public async Task Skapa_MedLagIAnnanTrupp_Ger400()
    {
        var trupp = await SeedTruppAsync("fel-lag");
        var annan = await SeedTruppAsync("annans-lag");

        var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/trupper/{trupp.AgeGroupId}/children",
            AdminToken(trupp.AgeGroupId),
            new { firstName = "Noah", lastInitial = "K", teamId = annan.TeamId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Andra_Barn_FlyttarMellanLag()
    {
        var trupp = await SeedTruppAsync("flytta");
        var otherTeamId = await SeedExtraTeamAsync(trupp.AgeGroupId, "bla-flytta");

        var id = await CreateChildAsync(trupp.AgeGroupId, "Ella", "S", trupp.TeamId);

        var update = await SendAsync(
            HttpMethod.Put, $"/api/v1/admin/trupper/{trupp.AgeGroupId}/children/{id}",
            AdminToken(trupp.AgeGroupId),
            new { firstName = "Ella", lastInitial = "S", teamId = otherTeamId });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var dto = await update.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        Assert.Equal(otherTeamId, dto.GetProperty("teamId").GetGuid());
    }

    [Fact]
    public async Task TaBort_Barn_ForsvinnerUrRoster()
    {
        var trupp = await SeedTruppAsync("bort");
        var id = await CreateChildAsync(trupp.AgeGroupId, "Vera", "L", null);

        var delete = await SendAsync(
            HttpMethod.Delete, $"/api/v1/admin/trupper/{trupp.AgeGroupId}/children/{id}",
            AdminToken(trupp.AgeGroupId));
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var roster = await RosterAsync(trupp.AgeGroupId);
        Assert.DoesNotContain(
            roster.GetProperty("children").EnumerateArray(), c => c.GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task Andra_Barn_IAnnanTrupp_Ger404()
    {
        var trupp = await SeedTruppAsync("idor");
        var annan = await SeedTruppAsync("idor-annan");
        var id = await CreateChildAsync(trupp.AgeGroupId, "Max", "B", null);

        // Adminen för den andra truppen försöker ändra ett barn i den första via sin adress.
        var response = await SendAsync(
            HttpMethod.Put, $"/api/v1/admin/trupper/{annan.AgeGroupId}/children/{id}",
            AdminToken(annan.AgeGroupId),
            new { firstName = "Max", lastInitial = "B", teamId = (Guid?)null });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Vårdnadshavarkoppling (samtyckesgrindad) ------------------------------------

    [Fact]
    public async Task Koppla_Vardnadshavare_UtanSamtycke_Ger409()
    {
        var trupp = await SeedTruppAsync("utan-samtycke");
        var childId = await CreateChildAsync(trupp.AgeGroupId, "Liam", "J", trupp.TeamId);

        // Medlem (accepterad inbjudan) men inget samtycke.
        var (_, email) = await SeedMemberAsync(trupp.AgeGroupId, "utan-samtycke", consent: false);

        var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/trupper/{trupp.AgeGroupId}/children/{childId}/guardians",
            AdminToken(trupp.AgeGroupId), new { email });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Koppla_Vardnadshavare_SomInteGattMed_Ger400()
    {
        var trupp = await SeedTruppAsync("ej-medlem");
        var childId = await CreateChildAsync(trupp.AgeGroupId, "Liam", "J", null);
        await SeedAccountAsync("utomstaende-vh@example.com");

        var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/trupper/{trupp.AgeGroupId}/children/{childId}/guardians",
            AdminToken(trupp.AgeGroupId), new { email = "utomstaende-vh@example.com" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Koppla_Vardnadshavare_MedSamtycke_SynsIRoster_OchAuditloggas()
    {
        var trupp = await SeedTruppAsync("med-samtycke");
        var childId = await CreateChildAsync(trupp.AgeGroupId, "Liam", "J", trupp.TeamId);
        var (accountId, email) = await SeedMemberAsync(trupp.AgeGroupId, "med-samtycke", consent: true);

        var link = await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/trupper/{trupp.AgeGroupId}/children/{childId}/guardians",
            AdminToken(trupp.AgeGroupId), new { email });
        Assert.Equal(HttpStatusCode.NoContent, link.StatusCode);
        await AssertAuditedAsync(AuditActions.GuardianLinked, childId);

        var roster = await RosterAsync(trupp.AgeGroupId);
        var child = roster.GetProperty("children").EnumerateArray()
            .Single(c => c.GetProperty("id").GetGuid() == childId);
        Assert.Contains(
            child.GetProperty("guardians").EnumerateArray(),
            g => g.GetProperty("accountId").GetGuid() == accountId);

        // Dubbelkoppling nekas.
        var again = await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/trupper/{trupp.AgeGroupId}/children/{childId}/guardians",
            AdminToken(trupp.AgeGroupId), new { email });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task KopplaBort_Vardnadshavare_Ger204()
    {
        var trupp = await SeedTruppAsync("bortkoppla");
        var childId = await CreateChildAsync(trupp.AgeGroupId, "Liam", "J", null);
        var (accountId, email) = await SeedMemberAsync(trupp.AgeGroupId, "bortkoppla", consent: true);

        await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/trupper/{trupp.AgeGroupId}/children/{childId}/guardians",
            AdminToken(trupp.AgeGroupId), new { email });

        var unlink = await SendAsync(
            HttpMethod.Delete,
            $"/api/v1/admin/trupper/{trupp.AgeGroupId}/children/{childId}/guardians/{accountId}",
            AdminToken(trupp.AgeGroupId));

        Assert.Equal(HttpStatusCode.NoContent, unlink.StatusCode);
        await AssertAuditedAsync(AuditActions.GuardianUnlinked, childId);
    }

    // ---- Hjälpare --------------------------------------------------------------------

    private sealed record Trupp(Guid AgeGroupId, Guid TeamId);

    private async Task<Trupp> SeedTruppAsync(string suffix)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var club = new Club { Id = Guid.NewGuid(), Name = "Kärra", Slug = $"klubb-b-{suffix}" };
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
            Slug = $"gul-b-{suffix}",
        };

        context.Clubs.Add(club);
        context.AgeGroups.Add(ageGroup);
        context.Teams.Add(team);
        await context.SaveChangesAsync(CancellationToken.None);

        return new Trupp(ageGroup.Id, team.Id);
    }

    private async Task<Guid> SeedExtraTeamAsync(Guid ageGroupId, string slug)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var team = new Team
        {
            Id = Guid.NewGuid(),
            AgeGroupId = ageGroupId,
            Name = "Blå",
            ColorHex = "#1B5FD9",
            Slug = slug,
        };
        context.Teams.Add(team);
        await context.SaveChangesAsync(CancellationToken.None);

        return team.Id;
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

    /// <summary>Ett konto som gått med i truppen (accepterad inbjudan), valfritt med samtycke.</summary>
    private async Task<(Guid AccountId, string Email)> SeedMemberAsync(
        Guid ageGroupId, string suffix, bool consent)
    {
        var email = $"vh-{suffix}@example.com";
        var accountId = await SeedAccountAsync(email);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        context.Invitations.Add(new Invitation
        {
            Id = Guid.NewGuid(),
            AgeGroupId = ageGroupId,
            Email = email,
            TokenHash = SessionIssuer.Hash(Guid.NewGuid().ToString()),
            CreatedByAccountId = Guid.NewGuid(),
            CreatedUtc = DateTime.UtcNow.AddDays(-1),
            ExpiresUtc = DateTime.UtcNow.AddDays(14),
            Status = InvitationStatus.Accepted,
            AcceptedByAccountId = accountId,
            AcceptedUtc = DateTime.UtcNow,
        });

        if (consent)
        {
            context.GuardianConsents.Add(new GuardianConsent
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                Version = ConsentDocument.CurrentVersion,
                GrantedUtc = DateTime.UtcNow,
            });
        }

        await context.SaveChangesAsync(CancellationToken.None);

        return (accountId, email);
    }

    private async Task<Guid> CreateChildAsync(
        Guid ageGroupId, string firstName, string lastInitial, Guid? teamId)
    {
        var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/trupper/{ageGroupId}/children",
            AdminToken(ageGroupId), new { firstName, lastInitial, teamId });
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        return dto.GetProperty("id").GetGuid();
    }

    private async Task<JsonElement> RosterAsync(Guid ageGroupId)
    {
        var response = await SendAsync(
            HttpMethod.Get, $"/api/v1/admin/trupper/{ageGroupId}/children", AdminToken(ageGroupId));
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

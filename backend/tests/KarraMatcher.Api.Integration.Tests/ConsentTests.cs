using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Application.Features.Consent;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Audit;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Vårdnadshavarsamtycke (§KM.6, `#195`): texten visas, samtycket sparas med version och
/// tidsstämpel, och det går att visa vad man samtyckte till. Krävs innan barn kopplas (`#196`).
/// </summary>
public sealed class ConsentTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private static WebApplicationFactoryClientOptions ClientOptions => new() { HandleCookies = true };

    [Fact]
    public async Task Current_UtanInloggning_Nekas()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/consent/current", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Current_SomInloggad_GerTextOchVersion()
    {
        var (accountId, email) = await SeedAccountAsync("current");

        var response = await SendAsync(HttpMethod.Get, "/api/v1/consent/current", TokenFor(accountId, email));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        Assert.Equal(ConsentDocument.CurrentVersion, dto.GetProperty("version").GetString());
        Assert.False(string.IsNullOrWhiteSpace(dto.GetProperty("text").GetString()));
    }

    [Fact]
    public async Task Samtycke_Sparas_GarAttVisa_OchAuditloggas()
    {
        var (accountId, email) = await SeedAccountAsync("grant");
        var token = TokenFor(accountId, email);

        // Före: inget samtycke.
        var before = await SendAsync(HttpMethod.Get, "/api/v1/consent/me", token);
        var beforeDto = await before.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        Assert.False(beforeDto.GetProperty("hasConsentedToCurrent").GetBoolean());

        var grant = await SendAsync(
            HttpMethod.Post, "/api/v1/consent", token,
            new { version = ConsentDocument.CurrentVersion });
        Assert.Equal(HttpStatusCode.NoContent, grant.StatusCode);
        await AssertAuditedAsync(AuditActions.ConsentGranted, accountId);

        // Efter: samtycke till aktuell version, med text och tidsstämpel.
        var after = await SendAsync(HttpMethod.Get, "/api/v1/consent/me", token);
        var afterDto = await after.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        Assert.True(afterDto.GetProperty("hasConsentedToCurrent").GetBoolean());
        Assert.Equal(ConsentDocument.CurrentVersion, afterDto.GetProperty("version").GetString());
        Assert.False(string.IsNullOrWhiteSpace(afterDto.GetProperty("text").GetString()));
        Assert.NotEqual(JsonValueKind.Null, afterDto.GetProperty("grantedUtc").ValueKind);
    }

    [Fact]
    public async Task Samtycke_MedFelVersion_Ger409()
    {
        var (accountId, email) = await SeedAccountAsync("stale");

        var response = await SendAsync(
            HttpMethod.Post, "/api/v1/consent", TokenFor(accountId, email),
            new { version = "0" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Samtycke_TvaGanger_ArIdempotent()
    {
        var (accountId, email) = await SeedAccountAsync("idempotent");
        var token = TokenFor(accountId, email);

        var first = await SendAsync(
            HttpMethod.Post, "/api/v1/consent", token, new { version = ConsentDocument.CurrentVersion });
        var second = await SendAsync(
            HttpMethod.Post, "/api/v1/consent", token, new { version = ConsentDocument.CurrentVersion });

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        var rows = await context.GuardianConsents.AsNoTracking()
            .CountAsync(c => c.AccountId == accountId, CancellationToken.None);
        Assert.Equal(1, rows);
    }

    // ---- Hjälpare --------------------------------------------------------------------

    private async Task<(Guid AccountId, string Email)> SeedAccountAsync(string suffix)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var email = $"{suffix}-consent@example.com";
        var account = new Account { Id = Guid.NewGuid(), Email = email, CreatedUtc = DateTime.UtcNow };

        context.Accounts.Add(account);
        await context.SaveChangesAsync(CancellationToken.None);

        return (account.Id, email);
    }

    private string TokenFor(Guid accountId, string email) =>
        TestAuth.TokenFor(factory.Services, accountId, AccountRoles.None, email);

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

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using KarraMatcher.Api.Diagnostics;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Domain.Audit;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// En audit-rad bär requestens correlation-id (§KM.10, `#204`). Det gör en känslig åtgärd
/// spårbar: id:t klienten fick i <c>X-Correlation-Id</c> är samma id som står på raden och i
/// loggarna, så ett ärende går att följa end-to-end.
/// </summary>
public sealed class AuditCorrelationTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private static WebApplicationFactoryClientOptions ClientOptions => new() { HandleCookies = true };

    [Fact]
    public async Task AuditRaden_BarKlientensCorrelationId()
    {
        const string correlationId = "corr-test-abc123";

        var (response, _) = await CreateSportAsync("fotboll-corr-klient", correlationId);

        var id = (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None))
            .GetProperty("id").GetGuid();

        // Klienten fick tillbaka precis det id den skickade in.
        Assert.Equal(correlationId, response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());

        Assert.Equal(correlationId, await StoredCorrelationIdAsync(AuditActions.SportCreated, id));
    }

    [Fact]
    public async Task AuditRaden_BarSamma_ServerGenererade_Id_SomSvaret()
    {
        // Utan ett id från klienten skapar servern ett — och samma id ska hamna både i
        // svarsheadern och på audit-raden, annars går kopplingen förlorad.
        var (response, _) = await CreateSportAsync("fotboll-corr-server", correlationId: null);

        var generated = response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single();
        Assert.False(string.IsNullOrWhiteSpace(generated));

        var id = (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None))
            .GetProperty("id").GetGuid();

        Assert.Equal(generated, await StoredCorrelationIdAsync(AuditActions.SportCreated, id));
    }

    private async Task<string?> StoredCorrelationIdAsync(string action, Guid subjectId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var entry = await context.AuditEntries.AsNoTracking()
            .SingleAsync(e => e.Action == action && e.SubjectId == subjectId, CancellationToken.None);

        return entry.CorrelationId;
    }

    private async Task<(HttpResponseMessage Response, string Token)> CreateSportAsync(
        string slug, string? correlationId)
    {
        var token = TestAuth.TokenFor(
            factory.Services, Guid.NewGuid(), new AccountRoles(true, [], []), "super@test");

        using var client = factory.CreateClient(ClientOptions);
        var (csrf, cookie) = await GetCsrfAsync(client, token);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/sports");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        request.Headers.Add("Cookie", cookie);

        if (correlationId is not null)
        {
            request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        }

        request.Content = JsonContent.Create(new { name = slug, slug });

        var response = await client.SendAsync(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();

        return (response, token);
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

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Passkeys (WebAuthn) — inloggning utan lösenord eller kod.
///
/// <para>
/// Själva kryptot ligger i Fido2NetLib (battle-tested); här prövas plumbingen och gränserna: att
/// options genereras för ett inloggat konto, att inloggnings-ceremonin är anonym men avvisar en
/// okänd utmaning, att man bara ser/tar bort sina egna, och att registrering/hantering kräver
/// inloggning. Den fulla ceremonin med en riktig autentiserare hör till E2E.
/// </para>
/// </summary>
public sealed class PasskeyTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private static WebApplicationFactoryClientOptions ClientOptions => new() { HandleCookies = true };

    private async Task<Guid> SeedAccountAsync(string suffix)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Email = $"pk-{suffix}@example.com",
            CreatedUtc = DateTime.UtcNow,
        };

        context.Accounts.Add(account);
        await context.SaveChangesAsync(CancellationToken.None);

        return account.Id;
    }

    private string TokenFor(Guid accountId) =>
        TestAuth.TokenFor(factory.Services, accountId, AccountRoles.None, "pk@example.com");

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, string? token, object? payload = null)
    {
        using var client = factory.CreateClient(ClientOptions);
        var (csrf, cookie) = await CsrfAsync(client, token);

        var request = new HttpRequestMessage(method, path);

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

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

    private static async Task<(string Token, string Cookie)> CsrfAsync(HttpClient client, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/csrf");

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await client.SendAsync(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        var cookie = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("karra_csrf", StringComparison.Ordinal))
            .Split(';')[0];

        return (body.GetProperty("token").GetString()!, cookie);
    }

    [Fact]
    public async Task Registrering_ForEttInloggatKonto_GerOptionsMedUtmaning()
    {
        var accountId = await SeedAccountAsync("reg");

        var response = await SendAsync(
            HttpMethod.Post, "/api/v1/passkeys/registrera/start", TokenFor(accountId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var options = await response.Content.ReadAsStringAsync(CancellationToken.None);
        Assert.Contains("challenge", options, StringComparison.Ordinal);
        Assert.Contains("localhost", options, StringComparison.Ordinal); // relying-party-id (förvalet)
    }

    [Fact]
    public async Task Inloggning_ArAnonym_OchGerEnUtmaning()
    {
        var response = await SendAsync(HttpMethod.Post, "/api/v1/passkeys/logga-in/start", token: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        Assert.False(string.IsNullOrEmpty(body.GetProperty("challengeId").GetString()));
        Assert.Contains("challenge", body.GetProperty("optionsJson").GetString()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Inloggning_MedOkandUtmaning_Nekas()
    {
        var response = await SendAsync(
            HttpMethod.Post,
            "/api/v1/passkeys/logga-in/klar",
            token: null,
            new { challengeId = "finns-inte", assertion = new { } });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Lista_UtanPasskeys_ArTom()
    {
        var accountId = await SeedAccountAsync("list");

        var response = await GetAsync("/api/v1/passkeys", TokenFor(accountId));
        response.EnsureSuccessStatusCode();

        var passkeys = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        Assert.Empty(passkeys.EnumerateArray());
    }

    [Fact]
    public async Task TaBort_NagotSomInteFinns_Ger404()
    {
        var accountId = await SeedAccountAsync("del");

        var response = await SendAsync(
            HttpMethod.Delete, $"/api/v1/passkeys/{Guid.NewGuid()}", TokenFor(accountId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Registrering_UtanInloggning_Nekas()
    {
        var response = await SendAsync(HttpMethod.Post, "/api/v1/passkeys/registrera/start", token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

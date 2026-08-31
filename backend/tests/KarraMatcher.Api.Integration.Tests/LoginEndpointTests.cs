using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using KarraMatcher.Application.Abstractions.Email;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Inloggningens HTTP-yta (checklistan 1.1, 1.2 och 1.7).
///
/// <para>
/// Reglerna bevisas i <c>LoginCodeServiceTests</c>. Det här bevisar att de överlever vägen
/// ut: att svaret ser likadant ut för en känd och en okänd adress, att en lyckad
/// verifiering faktiskt sätter sessionscookien, och att koden aldrig råkar följa med i
/// något svar.
/// </para>
/// </summary>
public sealed class LoginEndpointTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private static WebApplicationFactoryClientOptions ClientOptions => new() { HandleCookies = true };

    /// <summary>Fångar mejlet, så testet kan läsa koden som en förälder gör.</summary>
    private sealed class CapturingEmailSender : IEmailSender
    {
        public string? LastBody { get; private set; }

        public Task SendAsync(
            string recipient,
            string subject,
            string body,
            CancellationToken cancellationToken)
        {
            LastBody = body;

            return Task.CompletedTask;
        }
    }

    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/auth/csrf", CancellationToken.None);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        return body.GetProperty("token").GetString()!;
    }

    private static HttpRequestMessage Post(string path, string csrf, object payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(payload),
        };

        request.Headers.Add("X-CSRF-TOKEN", csrf);

        return request;
    }

    // ---- Vad svaret inte avslojar ----------------------------------------------------

    [Theory]
    [InlineData("finns-inte-alls@example.com")]
    [InlineData("kanske@example.com")]
    public async Task RequestCode_SvararAlltid202(string email)
    {
        // Samma svar oavsett om adressen är känd. Ett svar som skiljde på fallen hade
        // gjort inloggningsrutan till en adresslista.
        using var client = factory.CreateClient(ClientOptions);
        var csrf = await GetCsrfTokenAsync(client);

        var response = await client.SendAsync(
            Post("/api/v1/auth/request-code", csrf, new { email }),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task RequestCode_LamnarAldrigUtKodenISvaret()
    {
        using var client = factory.CreateClient(ClientOptions);
        var csrf = await GetCsrfTokenAsync(client);

        var response = await client.SendAsync(
            Post("/api/v1/auth/request-code", csrf, new { email = "foralder@example.com" }),
            CancellationToken.None);

        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.DoesNotMatch("[0-9]{6}", body);
    }

    [Fact]
    public async Task VerifyCode_FelKod_Ger401()
    {
        using var client = factory.CreateClient(ClientOptions);
        var csrf = await GetCsrfTokenAsync(client);

        var response = await client.SendAsync(
            Post(
                "/api/v1/auth/verify-code",
                csrf,
                new { email = "foralder@example.com", code = "000000" }),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task VerifyCode_TrasigKod_AvvisasSomValideringsfel()
    {
        // Formen kontrolleras före allt annat, så ett stavfel inte kostar ett försök.
        using var client = factory.CreateClient(ClientOptions);
        var csrf = await GetCsrfTokenAsync(client);

        var response = await client.SendAsync(
            Post(
                "/api/v1/auth/verify-code",
                csrf,
                new { email = "foralder@example.com", code = "abc" }),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- Hela vagen igenom -----------------------------------------------------------

    [Fact]
    public async Task Inloggning_FranBegaranTillSession()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var mailbox = new CapturingEmailSender();

        using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddScoped<IEmailSender>(_ => mailbox)));

        using var client = host.CreateClient(ClientOptions);
        var csrf = await GetCsrfTokenAsync(client);

        var requested = await client.SendAsync(
            Post("/api/v1/auth/request-code", csrf, new { email }),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);

        var code = new string(mailbox.LastBody!.Where(char.IsAsciiDigit).Take(6).ToArray());

        var verified = await client.SendAsync(
            Post("/api/v1/auth/verify-code", csrf, new { email, code }),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);

        // Sessionen ska landa i en httpOnly-cookie, precis som vid förnyelse.
        var setCookie = verified.Headers.GetValues("Set-Cookie")
            .Single(v => v.StartsWith("karra_refresh", StringComparison.Ordinal));

        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);

        var body = await verified.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("accessToken").GetString()));

        // Koden får aldrig komma tillbaka i svaret.
        var raw = await verified.Content.ReadAsStringAsync(CancellationToken.None);
        Assert.DoesNotContain(code, raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyCode_SammaKodTvaGanger_FungerarBaraForstaGangen()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var mailbox = new CapturingEmailSender();

        using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddScoped<IEmailSender>(_ => mailbox)));

        using var client = host.CreateClient(ClientOptions);
        var csrf = await GetCsrfTokenAsync(client);

        await client.SendAsync(
            Post("/api/v1/auth/request-code", csrf, new { email }),
            CancellationToken.None);

        var code = new string(mailbox.LastBody!.Where(char.IsAsciiDigit).Take(6).ToArray());

        var first = await client.SendAsync(
            Post("/api/v1/auth/verify-code", csrf, new { email, code }),
            CancellationToken.None);
        var second = await client.SendAsync(
            Post("/api/v1/auth/verify-code", csrf, new { email, code }),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }
}

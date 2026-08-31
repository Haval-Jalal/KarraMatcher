using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Sessionens HTTP-yta: cookien, CSRF-skyddet och rotationen (checklistan 1.3–1.6, 6.5).
///
/// <para>
/// Enhetstesterna i <c>SessionIssuerTests</c> bevisar reglerna. De här bevisar att de
/// överlever vägen ut genom HTTP — att cookien faktiskt får sina attribut, att
/// anti-forgery faktiskt krävs, och att refresh-token faktiskt inte råkar följa med i
/// svarskroppen. Det är den sortens sak som ser rätt ut i koden och blir fel i praktiken.
/// </para>
/// </summary>
public sealed class AuthEndpointTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private const string RefreshPath = "/api/v1/auth/refresh";
    private const string CookieName = "karra_refresh";

    private static WebApplicationFactoryClientOptions ClientOptions => new() { HandleCookies = true };

    /// <summary>Skapar ett konto och en riktig session, som om någon just loggat in.</summary>
    private async Task<SessionTokens> IssueSessionAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        var issuer = scope.ServiceProvider.GetRequiredService<SessionIssuer>();

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid():N}@example.com",
            CreatedUtc = DateTime.UtcNow,
        };

        context.Accounts.Add(account);
        await context.SaveChangesAsync(CancellationToken.None);

        return await issuer.IssueAsync(account, CancellationToken.None);
    }

    /// <summary>Hämtar en CSRF-token och sätter dess cookie på klienten.</summary>
    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/auth/csrf", CancellationToken.None);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        return body.GetProperty("token").GetString()!;
    }

    // ---- Cookien ---------------------------------------------------------------------

    [Fact]
    public async Task Refresh_SatterCookieMedRattSkydd()
    {
        var session = await IssueSessionAsync();
        using var client = factory.CreateClient(ClientOptions);
        var csrf = await GetCsrfTokenAsync(client);

        var request = new HttpRequestMessage(HttpMethod.Post, RefreshPath);
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        request.Headers.Add("Cookie", $"{CookieName}={session.RefreshToken}");

        var response = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var setCookie = response.Headers.GetValues("Set-Cookie")
            .Single(v => v.StartsWith(CookieName, StringComparison.Ordinal));

        // httpOnly: oåtkomlig för JavaScript, alltså värdelös för ett XSS-fönster.
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);

        // secure: aldrig över klartext.
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);

        // Lax: skyddar mot cross-site utan att en delad länk tappar inloggningen.
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);

        // Skickas bara till inloggningens egna endpoints, inte till schemat.
        Assert.Contains("path=/api/v1/auth", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Refresh_LamnarAldrigUtRefreshTokenIKroppen()
    {
        // Cookien är hela poängen. En token i kroppen kan hamna i localStorage, i en logg
        // eller i en felrapport — och då spelar httpOnly ingen roll.
        var session = await IssueSessionAsync();
        using var client = factory.CreateClient(ClientOptions);
        var csrf = await GetCsrfTokenAsync(client);

        var request = new HttpRequestMessage(HttpMethod.Post, RefreshPath);
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        request.Headers.Add("Cookie", $"{CookieName}={session.RefreshToken}");

        var response = await client.SendAsync(request, CancellationToken.None);
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.DoesNotContain(session.RefreshToken, body, StringComparison.Ordinal);
        Assert.DoesNotContain("refreshToken", body, StringComparison.OrdinalIgnoreCase);
    }

    // ---- CSRF ------------------------------------------------------------------------

    [Fact]
    public async Task Refresh_UtanCsrfToken_Nekas()
    {
        // Utan det här kan en annan webbplats få webbläsaren att förnya sessionen åt sig.
        var session = await IssueSessionAsync();
        using var client = factory.CreateClient(ClientOptions);

        var request = new HttpRequestMessage(HttpMethod.Post, RefreshPath);
        request.Headers.Add("Cookie", $"{CookieName}={session.RefreshToken}");

        var response = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_MedFelCsrfToken_Nekas()
    {
        var session = await IssueSessionAsync();
        using var client = factory.CreateClient(ClientOptions);
        await GetCsrfTokenAsync(client);

        var request = new HttpRequestMessage(HttpMethod.Post, RefreshPath);
        request.Headers.Add("X-CSRF-TOKEN", "inte-en-riktig-token");
        request.Headers.Add("Cookie", $"{CookieName}={session.RefreshToken}");

        var response = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Csrf_GerEnTokenUtanInloggning()
    {
        // Måste gå att hämta innan man är inloggad — annars går det inte att logga in.
        using var client = factory.CreateClient(ClientOptions);

        var token = await GetCsrfTokenAsync(client);

        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    // ---- Rotation genom HTTP ---------------------------------------------------------

    [Fact]
    public async Task Refresh_UtanCookie_Ger401()
    {
        using var client = factory.CreateClient(ClientOptions);
        var csrf = await GetCsrfTokenAsync(client);

        var request = new HttpRequestMessage(HttpMethod.Post, RefreshPath);
        request.Headers.Add("X-CSRF-TOKEN", csrf);

        var response = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_MisslyckadFornyelse_RensarCookien()
    {
        // En kvarliggande värdelös cookie får klienten att försöka i all evighet.
        using var client = factory.CreateClient(ClientOptions);
        var csrf = await GetCsrfTokenAsync(client);

        var request = new HttpRequestMessage(HttpMethod.Post, RefreshPath);
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        request.Headers.Add("Cookie", $"{CookieName}=en-token-som-inte-finns");

        var response = await client.SendAsync(request, CancellationToken.None);

        var setCookie = response.Headers.GetValues("Set-Cookie")
            .Single(v => v.StartsWith(CookieName, StringComparison.Ordinal));

        Assert.Contains("expires=", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Logout_SvararAlltid204()
    {
        // Även utan giltig session: annars går utloggningen att använda för att ta reda
        // på om en token är giltig.
        using var client = factory.CreateClient(ClientOptions);
        var csrf = await GetCsrfTokenAsync(client);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        request.Headers.Add("Cookie", $"{CookieName}=en-token-som-inte-finns");

        var response = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // ---- Tokenvalidering -------------------------------------------------------------

    [Fact]
    public void JwtBearer_ValiderarUtfardareMottagareLivstidOchSignatur()
    {
        // Checklistan 1.3. En avstängd kontroll ska kräva att någon aktivt skriver false.
        var options = factory.Services
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        var parameters = options.TokenValidationParameters;

        Assert.True(parameters.ValidateIssuer);
        Assert.True(parameters.ValidateAudience);
        Assert.True(parameters.ValidateLifetime);
        Assert.True(parameters.ValidateIssuerSigningKey);
        Assert.NotNull(parameters.IssuerSigningKey);
        Assert.False(string.IsNullOrWhiteSpace(parameters.ValidIssuer));
        Assert.False(string.IsNullOrWhiteSpace(parameters.ValidAudience));
    }

    // ---- Att skyddet sitter dar det ska ------------------------------------------------

    [Fact]
    public void VarjeTillstandsandrandeAuthEndpoint_KraverCsrfToken()
    {
        /*
         * Vakten mot att nagon lagger till en ny endpoint under /auth och glommer
         * attributet. Ett anrop i taget hade bara provat de tva som finns i dag.
         *
         * GET undantas: de andrar inget, och csrf-endpointen maste ga att na utan token
         * eftersom det ar den som delar ut den.
         */
        var unprotected = factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => (e.RoutePattern.RawText ?? string.Empty)
                .StartsWith("api/v1/auth", StringComparison.Ordinal))
            .Where(e => e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods
                .Any(m => m is not "GET" and not "HEAD" and not "OPTIONS") == true)
            .Where(e => e.Metadata.GetMetadata<RequireCsrfTokenAttribute>() is null)
            .Select(e => e.RoutePattern.RawText)
            .ToArray();

        Assert.True(
            unprotected.Length == 0,
            "Endpoint under /auth andrar tillstand utan CSRF-skydd (checklistan 6.5): "
                + string.Join(", ", unprotected));
    }

    [Fact]
    public void CsrfCookien_KraverHttpsUtanforUtveckling()
    {
        // I drift ska den vagra sattas over klartext. Lokalt maste den fa det, annars
        // kastar antiforgery -- vilket kostade en felsokning att upptacka.
        Assert.Equal(
            CookieSecurePolicy.Always,
            AuthenticationSetup.SecurePolicyFor(isDevelopment: false));

        Assert.Equal(
            CookieSecurePolicy.SameAsRequest,
            AuthenticationSetup.SecurePolicyFor(isDevelopment: true));
    }
}

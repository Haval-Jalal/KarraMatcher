using System.Net;
using System.Net.Http.Headers;

using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Security;
using KarraMatcher.Application.Features.Auth;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Behörighetsreglerna, prövade genom hela pipelinen (§KM.3, checklistan 2.1 och 2.6).
///
/// <para>
/// Tränarens riktiga endpoints byggs i M3. För att reglerna ska gå att pröva <em>nu</em> —
/// alltså innan det finns kod som fungerar utan dem — mappas en liten sondcontroller in i
/// testvärden. Den finns bara här och når aldrig den byggda appen.
/// </para>
///
/// <para>
/// Att pröva genom HTTP och inte bara mot <c>IAuthorizationService</c> är avsiktligt: det
/// är i pipelinen felen sitter. En policy kan vara felfri och ändå aldrig köras, om
/// <c>UseAuthentication</c> hamnat efter <c>UseAuthorization</c> eller attributet stavats
/// fel.
/// </para>
/// </summary>
public sealed class AuthorizationTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    /// <summary>Testvärden, med sondcontrollern inmappad.</summary>
    private WebApplicationFactory<Program> CreateHost() =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddControllers().AddApplicationPart(typeof(ProbeController).Assembly)));

    /// <summary>
    /// Utfärdar en riktig token med valda roller — samma väg som en inloggning.
    ///
    /// <para>
    /// I ett eget scope: utfärdaren är scoped, och att hämta den ur rotproviderns
    /// livstid är ett fel som ramverket vägrar utföra. Det är samma kontroll som skyddar
    /// mot att en DbContext råkar bli långlivad.
    /// </para>
    /// </summary>
    private static string TokenFor(IServiceProvider services, AccountRoles roles)
    {
        using var scope = services.CreateScope();
        var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();

        return issuer.Issue(Guid.NewGuid(), "foralder@example.com", roles).Token;
    }

    private static HttpClient Authenticated(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    // ---- Tranare ar bunden till sitt lag ---------------------------------------------

    [Fact]
    public async Task TranareForGul_NarSittEgetLag()
    {
        using var host = CreateHost();
        using var client = Authenticated(
            host.CreateClient(),
            TokenFor(host.Services, new AccountRoles(IsAdmin: false, CoachOf: ["gul"])));

        var response = await client.PostAsync("/probe/teams/gul/matches", null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TranareForGul_NarInteBlasLag()
    {
        /*
         * Karnan i hela issuen. Anroparen har en giltig token, en giltig roll och en
         * giltig adress -- och ska anda nekas. Utan objektnivakontrollen (checklistan
         * 2.6) ser allt korrekt ut anda fram till att nagon andrar ett annat lags match.
         */
        using var host = CreateHost();
        using var client = Authenticated(
            host.CreateClient(),
            TokenFor(host.Services, new AccountRoles(IsAdmin: false, CoachOf: ["gul"])));

        var response = await client.PostAsync("/probe/teams/bla/matches", null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task InloggadForalderUtanRoll_Nekas()
    {
        using var host = CreateHost();
        using var client = Authenticated(
            host.CreateClient(),
            TokenFor(host.Services, AccountRoles.None));

        var response = await client.PostAsync("/probe/teams/gul/matches", null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UtanToken_KraverInloggning()
    {
        using var host = CreateHost();
        using var client = host.CreateClient();

        var response = await client.PostAsync("/probe/teams/gul/matches", null, CancellationToken.None);

        // 401 och inte 403: skillnaden mellan "vem ar du" och "du far inte".
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Admin ----------------------------------------------------------------------

    [Theory]
    [InlineData("gul")]
    [InlineData("bla")]
    [InlineData("svart")]
    public async Task Admin_NarAllaLag(string slug)
    {
        using var host = CreateHost();
        using var client = Authenticated(
            host.CreateClient(),
            TokenFor(host.Services, new AccountRoles(IsAdmin: true, CoachOf: [])));

        var response = await client.PostAsync(
            $"/probe/teams/{slug}/matches", null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Tranare_NarInteAdminsEndpoints()
    {
        // Tränarrollen är inte en svagare admin. Den är något annat.
        using var host = CreateHost();
        using var client = Authenticated(
            host.CreateClient(),
            TokenFor(host.Services, new AccountRoles(IsAdmin: false, CoachOf: ["gul"])));

        var response = await client.GetAsync("/probe/admin", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- Kravet nar det inte hittar sitt lag -----------------------------------------

    [Fact]
    public async Task EndpointUtanLagIAdressen_Nekas()
    {
        /*
         * Det viktigaste utfallet i hela filen. En endpoint som bar kravet men saknar
         * {slug} ska falla, inte slappa igenom.
         *
         * Ett krav som inte hittar sitt lag och darfor godkanner vore varre an inget krav
         * alls: det ser ut att skydda, och den som lagger till endpointen far inget besked
         * om att den ar oskyddad.
         */
        using var host = CreateHost();
        using var client = Authenticated(
            host.CreateClient(),
            TokenFor(host.Services, new AccountRoles(IsAdmin: false, CoachOf: ["gul"])));

        var response = await client.PostAsync("/probe/glomt-lag", null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- Gasten ar orord --------------------------------------------------------------

    [Fact]
    public async Task Schemat_ArFortfarandeOppetUtanToken()
    {
        // Policyerna får inte ha smugit sig på den publika delen. GuestAccessTests vaktar
        // detta bredare; den här raden fångar det i samma fil som infört reglerna.
        using var host = CreateHost();
        using var client = host.CreateClient();

        var response = await client.GetAsync("/api/v1/teams", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

/// <summary>
/// Endpoints som bara finns i testvärden, för att pröva policyerna innan tränarens
/// riktiga endpoints byggs i M3.
/// </summary>
[ApiController]
[Route("probe")]
public sealed class ProbeController : ControllerBase
{
    [HttpPost("teams/{slug}/matches")]
    [Authorize(Policy = AuthorizationPolicies.CoachOfTeam)]
    public IActionResult EditTeamMatch(string slug) => Ok(new { slug });

    /// <summary>Med flit utan <c>{slug}</c> — se testet om kravet utan lag i adressen.</summary>
    [HttpPost("glomt-lag")]
    [Authorize(Policy = AuthorizationPolicies.CoachOfTeam)]
    public IActionResult MissingSlug() => Ok();

    [HttpGet("admin")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    public IActionResult AdminOnly() => Ok();
}

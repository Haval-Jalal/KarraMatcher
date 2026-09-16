using System.Net;
using System.Text.Json;

using KarraMatcher.Domain.Matches;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Den stängda appen (§KM.3, v2 `#191`): en gäst utan inloggning ser ingenting.
///
/// <para>
/// I v1 var det motsatt — schemat var öppet och testet vaktade att inget råkade bli
/// <em>stängt</em>. I v2 vändes kravet: allt innehåll (matcher, träningar, samåkning,
/// notiser) kräver en inloggad medlem, och den vanligaste risken är nu att något råkar bli
/// <em>öppet</em> — en ny endpoint utan <c>[Authorize]</c>, en glömd policy. Därför vaktar
/// testet att varje endpoint under <c>api/</c> kräver auktorisering, med undantag bara för
/// en kort, försvarbar lista (inloggningen själv och cron-jobbet).
/// </para>
///
/// <para>
/// Två sorters kontroll, eftersom de fångar olika fel. HTTP-anropen visar att en gäst
/// faktiskt nekas i dag. Kontrollen av routernas metadata visar <em>varför</em>, och fäller
/// bygget den dag någon lägger till en oskyddad route — även om ingen råkar köra just det
/// anropet.
/// </para>
/// </summary>
public sealed class GuestAccessTests : IClassFixture<KarraMatcherApiFactory>
{
    private static readonly DateTime Kickoff = new(2026, 9, 5, 12, 30, 0, DateTimeKind.Utc);

    /// <summary>Notis med sådant som aldrig får lämna servern (§KM.1, ADR 2026-08-30).</summary>
    private const string SecretNote = "Ring Elias mamma om samakning";

    private readonly KarraMatcherApiFactory _factory;
    private readonly Guid _matchId;

    public GuestAccessTests(KarraMatcherApiFactory factory)
    {
        _factory = factory;
        _matchId = Seed(factory);
    }

    private static Guid Seed(KarraMatcherApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var existing = context.Matches.FirstOrDefault(m => m.Note != null);

        if (existing is not null)
        {
            return existing.Id;
        }

        var club = new Club { Id = Guid.NewGuid(), Name = "Karra KIF", Slug = "karra-kif-gast" };
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
            Name = "Gast",
            ColorHex = "#D9A21B",
            Slug = "gast",
        };
        var venue = new Venue
        {
            Id = Guid.NewGuid(),
            Name = "Karra IP",
            Address = "Idrottsvagen 1, Goteborg",
            Latitude = 57.79,
            Longitude = 11.94,
            IsHome = true,
        };
        var match = new Match
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            KickoffUtc = Kickoff,
            OpponentName = "Torslanda",
            VenueId = venue.Id,
            IsHome = true,
            Status = MatchStatus.Scheduled,
            Note = SecretNote,
            UpdatedUtc = Kickoff,
        };

        context.Clubs.Add(club);
        context.AgeGroups.Add(ageGroup);
        context.Teams.Add(team);
        context.Venues.Add(venue);
        context.Matches.Add(match);
        context.SaveChanges();

        return match.Id;
    }

    /// <summary>Innehållsytan som numera kräver inloggning — allt en gäst förr kunde se.</summary>
    public static TheoryData<string> ClosedPathTemplates =>
        [
            "/api/v1/teams",
            "/api/v1/teams/gast/matches",
            "/api/v1/matches/{0}",
            "/api/v1/matches/{0}/carpool/offers",
            "/api/v1/push/key",
        ];

    // ---- En gäst nekas ---------------------------------------------------------------

    [Theory]
    [MemberData(nameof(ClosedPathTemplates))]
    public async Task Gast_UtanToken_Nekas(string template)
    {
        // Stängd app (§KM.3): utan token är svaret 401 på hela innehållsytan.
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(
            string.Format(System.Globalization.CultureInfo.InvariantCulture, template, _matchId),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Gast_MedTrasigToken_Nekas()
    {
        // En gammal eller trasig token i en telefon som legat i fickan sedan förra säsongen
        // autentiserar inte — och en gäst ska mötas av 401, inte av ett tyst fel.
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer inte-en-riktig-token");

        var response = await client.GetAsync("/api/v1/teams", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Varför den är stängd --------------------------------------------------------

    [Fact]
    public void VarjeApiEndpoint_UtomAllowlist_KraverAuktorisering()
    {
        /*
         * Testet som faller bygget nar en ny endpoint glommer sitt [Authorize]. Det behover
         * inget anrop och marker aven en route som ingen rakar testa.
         *
         * I den stangda appen ar detta bade las- och skrivvakten i ett: allt under api/ ska
         * krava auktorisering, utom den korta allow-listan (inloggningen sjalv och cron).
         */
        var open = AllRouteEndpoints()
            .Where(e => !IsAnonymousByDesign(e))
            .Where(e => e.Metadata.GetOrderedMetadata<IAuthorizeData>().Count == 0)
            .Select(e => $"{Methods(e)} {e.RoutePattern.RawText}")
            .ToArray();

        Assert.True(
            open.Length == 0,
            "Endpoints saknar krav på inloggning i den stängda appen (§KM.3): "
                + string.Join(", ", open));
    }

    [Fact]
    public void Auktorisering_HarIngenFallbackPolicy()
    {
        // Ingen fallback-policy behövs — varje endpoint säger sitt eget krav. Kontrollen
        // finns kvar från v1: en fallback vore ett trubbigt sätt att låsa allt på en rad,
        // och skulle dölja en enskild endpoint som glömt sitt [Authorize].
        var options = _factory.Services.GetService<IOptions<AuthorizationOptions>>();

        Assert.Null(options?.Value.FallbackPolicy);
    }

    /// <summary>
    /// Endpoints som är öppna med avsikt.
    ///
    /// <para>
    /// Alla hör till inloggningen själv (man kan inte kräva en session för att skapa en)
    /// eller till cron-jobbet (anroparen är Vercels cron, inte en människa — det skyddas av
    /// en delad hemlighet i stället). Listan är kort med flit: varje rad är ett undantag
    /// någon måste kunna försvara.
    /// </para>
    /// </summary>
    private static bool IsAnonymousByDesign(RouteEndpoint endpoint)
    {
        string[] allowed =
        [
            /*
             * Anti-forgery-token: double-submit-halvan som klienten laser innan den kan
             * skriva. Maste ga att hamta innan sessionen ar helt pa plats -- den ar en del
             * av inloggningens bootstrap, inte innehall.
             */
            "api/v1/auth/csrf",

            "api/v1/auth/request-code",
            "api/v1/auth/verify-code",
            "api/v1/auth/refresh",
            "api/v1/auth/logout",

            /*
             * Kvallspaminnelsens jobb (#64). Anroparen ar Vercels cron, inte en manniska --
             * det finns ingen session att krava. Skrivningen skyddas av en delad hemlighet i
             * stallet, kontrollerad i JobsController med konstant tid.
             */
            "api/v1/jobs/match-reminders",
        ];

        return allowed.Contains(endpoint.RoutePattern.RawText, StringComparer.Ordinal);
    }

    private IEnumerable<RouteEndpoint> AllRouteEndpoints() =>
        _factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => (e.RoutePattern.RawText ?? string.Empty)
                .StartsWith("api/", StringComparison.Ordinal));

    private static string Methods(RouteEndpoint endpoint) =>
        string.Join("/", endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? []);

    // ---- Inga personuppgifter i svaren ------------------------------------------------

    [Theory]
    [InlineData("/api/v1/teams")]
    [InlineData("/api/v1/teams/gast/matches")]
    public async Task Svar_InnehallerIngaPersonuppgifter(string path)
    {
        // Inte ens för en inloggad medlem läcker barn-PII (§KM.1). Testet läser fältnamnen.
        using var client = _factory.CreateSuperAdminClient();

        var response = await client.GetAsync(path, CancellationToken.None);
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);
        var names = PropertyNames(JsonDocument.Parse(body).RootElement);

        var offenders = names.Where(IsPersonalData).ToArray();

        Assert.True(
            offenders.Length == 0,
            "Svar innehåller fält som ser ut som personuppgifter (§KM.1): "
                + string.Join(", ", offenders));
    }

    [Fact]
    public async Task MatchSvar_LamnarAldrigUtNotisen()
    {
        // Notisen är tränarens fritext och räknas som potentiell PII (ADR 2026-08-30) — den
        // får inte finnas i svaret, inte ens för en inloggad medlem.
        using var client = _factory.CreateSuperAdminClient();

        var response = await client.GetAsync($"/api/v1/matches/{_matchId}", CancellationToken.None);
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.DoesNotContain("Elias", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(SecretNote, body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Fältnamn som inte får förekomma i ett svar.
    ///
    /// <para>
    /// Jämförelsen sker mot <em>fältnamn</em> och inte mot hela svarskroppen. En
    /// textsökning hade slagit larm på en motståndare som heter Notviken eller en gata som
    /// heter Mailandsvagen — och ett test som ropar varg blir avstängt, inte fixat.
    /// </para>
    /// </summary>
    private static bool IsPersonalData(string name)
    {
        string[] forbidden =
        [
            "lastname", "surname", "efternamn",
            "personalnumber", "personnummer", "ssn",
            "birthdate", "dateofbirth", "fodelsedatum",
            "phone", "phonenumber", "telefon",
            "email", "epost",
            "photo", "picture", "foto",
            "health", "halsa",
            "guardian", "vardnadshavare",
            "note", "notis", "comment",
            "player", "players", "spelare",
        ];

        return forbidden.Contains(name.ToLowerInvariant(), StringComparer.Ordinal);
    }

    /// <summary>Alla fältnamn i ett JSON-svar, hur djupt de än ligger.</summary>
    private static IEnumerable<string> PropertyNames(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    yield return property.Name;

                    foreach (var nested in PropertyNames(property.Value))
                    {
                        yield return nested;
                    }
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var nested in PropertyNames(item))
                    {
                        yield return nested;
                    }
                }

                break;

            default:
                break;
        }
    }
}

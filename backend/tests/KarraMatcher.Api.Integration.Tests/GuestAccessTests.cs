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
/// Gästen behåller full åtkomst till den publika delen (§KM.3, §KM.0 A4).
///
/// <para>
/// Testerna är skrivna <em>innan</em> inloggningen byggs, och det är hela poängen. Det
/// vanligaste misstaget när auth införs är att för mycket blir skyddat: en global
/// fallback-policy, ett <c>[Authorize]</c> på fel klass, en middleware i fel ordning. Då
/// möts en förälder som bara vill se matchtiden av en inloggningsruta, och produktlöftet
/// är brutet utan att något ser trasigt ut.
/// </para>
///
/// <para>
/// Två sorters kontroll, eftersom de fångar olika fel. HTTP-anropen visar att ytan svarar
/// i dag. Kontrollen av routernas metadata visar <em>varför</em> den gör det, och fäller
/// bygget den dag någon skyddar en route som ska vara öppen — även om ingen råkar köra
/// just det anropet.
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

    /// <summary>Hela den yta som ska vara öppen för vem som helst med länken.</summary>
    public static TheoryData<string> PublicPathTemplates =>
        [
            "/api/v1/teams",
            "/api/v1/teams/gast/matches",
            "/api/v1/matches/{0}",
            "/calendar/gast.ics",
            "/calendar/match/{0}.ics",
        ];

    // ---- Åtkomst utan inloggning -----------------------------------------------------

    [Theory]
    [MemberData(nameof(PublicPathTemplates))]
    public async Task PublikEndpoint_UtanToken_KraverIngenInloggning(string template)
    {
        // Assertionen är att svaret inte är 401 eller 403 — alltså exakt det en införd
        // inloggning skulle göra fel. Ett 404 hade varit ett annat fel och fångas nedan.
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(
            string.Format(System.Globalization.CultureInfo.InvariantCulture, template, _matchId),
            CancellationToken.None);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(PublicPathTemplates))]
    public async Task PublikEndpoint_UtanToken_Svarar200(string template)
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(
            string.Format(System.Globalization.CultureInfo.InvariantCulture, template, _matchId),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PublikEndpoint_MedTrasigToken_SlapperAndaIgenom()
    {
        // En gammal eller trasig token i en telefon som legat i fickan sedan förra
        // säsongen får inte göra schemat oläsbart. Anonymt och trasigt ska bete sig lika.
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer inte-en-riktig-token");

        var response = await client.GetAsync("/api/v1/teams", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---- Varför den är öppen ---------------------------------------------------------

    [Fact]
    public void PublikLasning_HarIngenAuktoriseringsmetadata()
    {
        /*
         * Testet som faller bygget nar inloggningen infors fel. Det behover inget anrop
         * och marker aven en route som ingen rakar testa.
         *
         * Bara sakra metoder raknas. §KM.3 ar "publik lasning, autentiserad skrivning" --
         * en POST under samma adress ar alltsa inte den publika ytan. Forsta versionen av
         * det har testet matchade pa sokvag oavsett metod, och fallde nar tranarens
         * endpoints kom i #35. Den hade ratt att titta, men fel upplosning.
         */
        var protectedReads = PublicRouteEndpoints()
            .Where(IsSafeMethod)
            .Where(e => e.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0)
            .Select(e => e.RoutePattern.RawText)
            .ToArray();

        Assert.True(
            protectedReads.Length == 0,
            "Publik läsning har fått krav på inloggning, vilket bryter §KM.3: "
                + string.Join(", ", protectedReads));
    }

    [Fact]
    public void Skrivning_ArAldrigOppen()
    {
        /*
         * Den omvanda kontrollen, och lika viktig. §KM.3 kraver inloggning for allt som
         * skriver -- en oskyddad POST under en publik adress ar precis lika illa som en
         * skyddad GET, och betydligt lattare att skriva av misstag.
         */
        /*
         * Varje route, inte bara de under de publika prefixen. Forsta versionen tittade
         * bara dar den publika lasningen bor, och hade darfor missat en oskyddad
         * /api/v1/venues -- alltsa precis det slags nya endpoint kontrollen finns for.
         */
        var openWrites = AllRouteEndpoints()
            .Where(e => !IsSafeMethod(e))
            .Where(e => !IsAnonymousByDesign(e))
            .Where(e => e.Metadata.GetOrderedMetadata<IAuthorizeData>().Count == 0)
            .Select(e => $"{Methods(e)} {e.RoutePattern.RawText}")
            .ToArray();

        Assert.True(
            openWrites.Length == 0,
            "Endpoints som ändrar tillstånd saknar krav på inloggning (§KM.3): "
                + string.Join(", ", openWrites));
    }

    /// <summary>
    /// Endpoints som ändrar tillstånd utan inloggning, med avsikt.
    ///
    /// <para>
    /// Alla hör till inloggningen själv: man kan inte kräva en session för att skapa en.
    /// Listan är kort med flit — varje rad här är ett undantag någon måste kunna försvara.
    /// </para>
    /// </summary>
    private static bool IsAnonymousByDesign(RouteEndpoint endpoint)
    {
        string[] allowed =
        [
            "api/v1/auth/request-code",
            "api/v1/auth/verify-code",
            "api/v1/auth/refresh",
            "api/v1/auth/logout",
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

    private static bool IsSafeMethod(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods
            .All(method => method is "GET" or "HEAD" or "OPTIONS") ?? true;

    [Fact]
    public void Auktorisering_HarIngenFallbackPolicy()
    {
        // En fallback-policy skyddar allt som inte uttryckligen sagt något annat, och är
        // det snabbaste sättet att låsa hela den publika delen med en rad. Införs auth ska
        // varje skyddad endpoint säga det själv.
        var options = _factory.Services.GetService<IOptions<AuthorizationOptions>>();

        Assert.True(
            options?.Value.FallbackPolicy is null,
            "En fallback-policy är satt. Den låser den publika delen (§KM.3) — "
                + "skydda endpoints var för sig i stället.");
    }

    private IEnumerable<RouteEndpoint> PublicRouteEndpoints()
    {
        var open = new[] { "api/v1/teams", "api/v1/matches", "calendar" };

        return _factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => open.Any(prefix =>
                (e.RoutePattern.RawText ?? string.Empty).StartsWith(prefix, StringComparison.Ordinal)));
    }

    // ---- Inga personuppgifter i publika svar ------------------------------------------

    [Theory]
    [InlineData("/api/v1/teams")]
    [InlineData("/api/v1/teams/gast/matches")]
    public async Task PubliktSvar_InnehallerIngaPersonuppgifter(string path)
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(path, CancellationToken.None);
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);
        var names = PropertyNames(JsonDocument.Parse(body).RootElement);

        var offenders = names.Where(IsPersonalData).ToArray();

        Assert.True(
            offenders.Length == 0,
            "Publikt svar innehåller fält som ser ut som personuppgifter (§KM.1): "
                + string.Join(", ", offenders));
    }

    [Fact]
    public async Task MatchSvar_LamnarAldrigUtNotisen()
    {
        // Notisen är tränarens fritext och räknas som potentiell PII (ADR 2026-08-30).
        // Publika svar cachas dessutom på Vercels edge.
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/matches/{_matchId}", CancellationToken.None);
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.DoesNotContain("Elias", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(SecretNote, body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IcsFeed_LamnarAldrigUtNotisen()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/calendar/gast.ics", CancellationToken.None);
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        // §KM.4: feeden innehåller matchdata och ingenting annat.
        Assert.DoesNotContain("Elias", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(SecretNote, body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Fältnamn som inte får förekomma i ett publikt svar.
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

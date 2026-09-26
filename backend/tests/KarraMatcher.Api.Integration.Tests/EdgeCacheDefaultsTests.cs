using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Den säkra standarden, verifierad mot den <em>skarpa</em> appen och inte mot en
/// testvärd. Ingen endpoint är markerad som publikt cachebar än — schema, matchdetalj
/// och ICS-feed byggs i M1 — så allt API:t svarar med i dag ska vara omöjligt att cacha.
/// </summary>
public class EdgeCacheDefaultsTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    [Theory]
    [InlineData("/")]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    public async Task IngenEndpoint_KanCachasAvEdge(string path)
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path, CancellationToken.None);
        var cacheControl = response.Headers.CacheControl;

        Assert.NotNull(cacheControl);
        Assert.True(cacheControl.NoStore, $"{path} saknar no-store");
        Assert.False(cacheControl.Public, $"{path} är märkt public");
        Assert.Null(cacheControl.SharedMaxAge);
        Assert.Null(response.Headers.ETag);
    }

    [Fact]
    public async Task ApplikationensEgenEndpoint_FarMellanvaransStandard()
    {
        // Rotsvaret har ingen egen åsikt om cachning, så mellanvarans standard gäller
        // rakt av. Det är den vägen varje framtida endpoint tar tills någon medvetet
        // markerar den som publik.
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/", CancellationToken.None);
        var cacheControl = response.Headers.CacheControl;

        Assert.NotNull(cacheControl);
        Assert.True(cacheControl.Private, "private saknas");
        Assert.True(cacheControl.NoStore, "no-store saknas");
    }

    [Fact]
    public async Task Halsokontrollen_BehallerSinEgenHeader()
    {
        // ASP.NET:s health check-middleware sätter själv no-store, no-cache. Mellanvaran
        // skriver aldrig över en handler som redan sagt sitt — resultatet blir detsamma,
        // och pingen från uppetidsverktyget får aldrig ett cachat svar som skulle dölja
        // en nere backend (§KM.11).
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health", CancellationToken.None);
        var cacheControl = response.Headers.CacheControl;

        Assert.NotNull(cacheControl);
        Assert.True(cacheControl.NoStore, "no-store saknas");
        Assert.True(cacheControl.NoCache, "no-cache saknas");
    }

    [Fact]
    public async Task VarjeGetEndpoint_SvararNoStore()
    {
        /*
         * Uttömmande: de tre InlineData-raderna ovan vaktar mellanvarans standard och
         * halsokontrollens undantag, men de raknar inte upp API:t. Den har gar over *varje*
         * GET-route under api/ och kraver no-store pa svaret -- aven en 401 eller 404 passerar
         * samma mellanvara, sa den fangar den dag nagon lagger till en endpoint som med flit
         * eller av misstag satter public/s-maxage. I den stangda appen (§KM.3/§KM.11 v2) finns
         * ingen publik edge-cache kvar; det ar precis det som ska forbli sant har.
         *
         * Bara GET provas -- en POST kan inte anropas utan giltig body/CSRF -- men no-store-
         * mellanvaran ar metodoberoende, sa GET-svepet racker for att fanga en felmarkerad
         * route. Ruttparametrar fylls med ett gissat varde; svaret blir da 401/404, vilket ar
         * oviktigt: det enda som provas ar cache-huvudet.
         */
        using var client = factory.CreateClient();

        var getEndpoints = factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => (e.RoutePattern.RawText ?? string.Empty).StartsWith("api/", StringComparison.Ordinal))
            .Where(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Contains(HttpMethods.Get, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        Assert.NotEmpty(getEndpoints);

        var offenders = new List<string>();

        foreach (var endpoint in getEndpoints)
        {
            var path = "/" + FillRouteParameters(endpoint.RoutePattern.RawText!);
            var response = await client.GetAsync(path, CancellationToken.None);
            var cacheControl = response.Headers.CacheControl;

            if (cacheControl is null || !cacheControl.NoStore || cacheControl.Public || cacheControl.SharedMaxAge is not null)
            {
                offenders.Add($"{path} -> {cacheControl?.ToString() ?? "(inget Cache-Control)"}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "GET-endpoints saknar no-store eller är edge-cachebara (§KM.11 v2):\n" + string.Join('\n', offenders));
    }

    // Byter ut {param}, {param:constraint} och {param?} mot ett gissat värde så routen matchar
    // (eller ger en ren 404) — svarets cache-huvud är det enda som räknas här. Ett GUID duger
    // åt både :guid-begränsade och fria parametrar; text utanför klamrarna ({token}.ics) står kvar.
    private static string FillRouteParameters(string template) =>
        Regex.Replace(template, @"\{[^}]+\}", "11111111-1111-1111-1111-111111111111");
}

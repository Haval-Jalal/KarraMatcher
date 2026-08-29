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
}

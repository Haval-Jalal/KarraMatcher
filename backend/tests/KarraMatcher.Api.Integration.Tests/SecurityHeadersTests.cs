namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Säkerhetsheaders på API-svaren (säkerhetschecklistan 5.1, 5.2, 4.2).
///
/// <para>
/// Ett JSON-svar kan ändå ramas in, MIME-sniffas eller läcka en referer. Headrarna ska
/// finnas på varje svar, och HSTS bara när requesten kom över HTTPS — annars låser en
/// utvecklares webbläsare fast <c>localhost</c> vid HTTPS.
/// </para>
/// </summary>
public sealed class SecurityHeadersTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private async Task<HttpResponseMessage> GetAsync(string path, bool overHttps = false)
    {
        using var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, path);

        if (overHttps)
        {
            // Render avslutar TLS och satter X-Forwarded-Proto. Forwarded-headers-middlewaren
            // gor da context.Request.IsHttps sann.
            request.Headers.Add("X-Forwarded-Proto", "https");
        }

        return await client.SendAsync(request, CancellationToken.None);
    }

    [Fact]
    public async Task Svar_HarNosniffRamskyddOchRefererPolicy()
    {
        using var response = await GetAsync("/api/v1/teams");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
    }

    [Fact]
    public async Task Svar_HarRestriktivCsp()
    {
        using var response = await GetAsync("/api/v1/teams");

        var csp = response.Headers.GetValues("Content-Security-Policy").Single();

        Assert.Contains("default-src 'none'", csp, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", csp, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Svar_OverHttps_HarHsts()
    {
        using var response = await GetAsync("/api/v1/teams", overHttps: true);

        var hsts = response.Headers.GetValues("Strict-Transport-Security").Single();

        Assert.Contains("max-age=", hsts, StringComparison.Ordinal);
        Assert.Contains("includeSubDomains", hsts, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Svar_OverKlartext_SaknarHsts()
    {
        // Utan HTTPS ska HSTS inte skickas -- annars later en utvecklares webblasare fast
        // localhost vid HTTPS.
        using var response = await GetAsync("/api/v1/teams");

        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }

    [Fact]
    public async Task Aven_FelsvarBarSakerhetsheaders()
    {
        // En okand route ger 404 -- headrarna ska finnas anda, de satts i OnStarting.
        using var response = await GetAsync("/api/v1/finns-inte");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
    }
}

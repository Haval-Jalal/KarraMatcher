using System.Net;
using System.Net.Http.Headers;

using KarraMatcher.Api.Caching;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Mekanismen bakom §KM.11: publika GET-svar ska kunna besvaras av Vercels edge utan att
/// Render väcks, och allt annat ska vara omöjligt att cacha.
///
/// <para>
/// Testerna kör mellanvaran i en egen liten värd i stället för i hela API:t. Det är
/// avsiktligt: i dag finns ingen publik endpoint att markera — schema, matchdetalj och
/// ICS-feed byggs i M1 — och en mekanism utan konsument måste ändå bevisas fungera.
/// Att den <em>skarpa</em> appen får den säkra standarden verifieras separat längst ned.
/// </para>
/// </summary>
public class EdgeCacheTests
{
    private const string Payload = """{"lag":"Kärra P13","matcher":3}""";

    /// <summary>
    /// Startar en värd med samma mellanvara som produktionsappen, plus två endpoints:
    /// en markerad som publikt cachebar och en omarkerad.
    /// </summary>
    private static IHost CreateHost(Action<EdgeCacheOptions>? configure = null) =>
        new HostBuilder()
            .ConfigureWebHost(webHost => webHost
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddKarraEdgeCache(new ConfigurationBuilder().Build());

                    if (configure is not null)
                    {
                        services.Configure(configure);
                    }
                })
                .Configure((IApplicationBuilder app) =>
                {
                    app.UseRouting();
                    app.UseKarraEdgeCache();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/publikt", () => Results.Content(Payload, "application/json"))
                            .WithEdgeCache(EdgeCacheProfile.Schedule);

                        endpoints.MapGet("/kalender", () => Results.Content("BEGIN:VCALENDAR", "text/calendar"))
                            .WithEdgeCache(EdgeCacheProfile.Calendar);

                        endpoints.MapGet("/omarkerat", () => Results.Ok(new { hemligt = true }));

                        endpoints.MapPost("/publikt", () => Results.Ok())
                            .WithEdgeCache(EdgeCacheProfile.Schedule);

                        endpoints.MapGet("/trasigt", () => Results.NotFound())
                            .WithEdgeCache(EdgeCacheProfile.Schedule);

                        endpoints.MapGet("/med-cookie", (HttpContext context) =>
                        {
                            context.Response.Cookies.Append("session", "abc");
                            return Results.Ok(new { data = 1 });
                        }).WithEdgeCache(EdgeCacheProfile.Schedule);
                    });
                }))
            .Start();

    [Fact]
    public async Task PubliktSvar_HarSMaxAgeOchStaleWhileRevalidate()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();

        var response = await client.GetAsync("/publikt", CancellationToken.None);
        var cacheControl = response.Headers.CacheControl?.ToString();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(cacheControl);
        Assert.Contains("public", cacheControl, StringComparison.Ordinal);
        Assert.Contains("s-maxage=300", cacheControl, StringComparison.Ordinal);
        Assert.Contains("stale-while-revalidate=3600", cacheControl, StringComparison.Ordinal);

        // max-age=0 är avsiktligt: edge ska svara, men en flyttad match får inte ligga
        // kvar i en förälders telefon.
        Assert.Contains("max-age=0", cacheControl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Profil_StyrLivslangden()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();

        var schedule = await client.GetAsync("/publikt", CancellationToken.None);
        var calendar = await client.GetAsync("/kalender", CancellationToken.None);

        Assert.Contains(
            "s-maxage=300",
            schedule.Headers.CacheControl?.ToString(),
            StringComparison.Ordinal);
        Assert.Contains(
            "s-maxage=900",
            calendar.Headers.CacheControl?.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Konfiguration_AndrarLivslangden()
    {
        using var host = CreateHost(options => options.ScheduleSeconds = 42);
        using var client = host.GetTestClient();

        var response = await client.GetAsync("/publikt", CancellationToken.None);

        Assert.Contains(
            "s-maxage=42",
            response.Headers.CacheControl?.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task PubliktSvar_HarEtagOchOforandratInnehallGerSammaEtag()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();

        var first = await client.GetAsync("/publikt", CancellationToken.None);
        var second = await client.GetAsync("/publikt", CancellationToken.None);

        Assert.NotNull(first.Headers.ETag);
        Assert.Equal(first.Headers.ETag, second.Headers.ETag);
    }

    [Fact]
    public async Task IfNoneMatch_MedRattEtag_Ger304UtanKropp()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();

        var first = await client.GetAsync("/publikt", CancellationToken.None);
        var etag = first.Headers.ETag!.ToString();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/publikt");
        request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        var second = await client.SendAsync(request, CancellationToken.None);
        var body = await second.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
        Assert.Empty(body);

        // Headers måste följa med på 304 — annars tappar edge livslängden.
        Assert.NotNull(second.Headers.CacheControl);
    }

    [Fact]
    public async Task IfNoneMatch_MedFelEtag_Ger200OchInnehall()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/publikt");
        request.Headers.TryAddWithoutValidation("If-None-Match", "\"nagot-helt-annat\"");
        var response = await client.SendAsync(request, CancellationToken.None);
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Payload, body);
    }

    [Fact]
    public async Task OmarkeradEndpoint_ArPrivateNoStore()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();

        var response = await client.GetAsync("/omarkerat", CancellationToken.None);

        AssertNoStore(response);
    }

    [Fact]
    public async Task Post_CachasAldrig_AvenOmMarkerad()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();

        using var content = new StringContent(string.Empty);
        var response = await client.PostAsync("/publikt", content, CancellationToken.None);

        AssertNoStore(response);
    }

    [Fact]
    public async Task Felsvar_CachasAldrig()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();

        var response = await client.GetAsync("/trasigt", CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain(
            "public",
            response.Headers.CacheControl?.ToString() ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task SvarSomSatterCookie_CachasAldrig()
    {
        // Ett Set-Cookie betyder användarspecifikt innehåll. Skulle en handler någon gång
        // råka kombinera det med en publik markering får edge inte dela svaret vidare.
        using var host = CreateHost();
        using var client = host.GetTestClient();

        var response = await client.GetAsync("/med-cookie", CancellationToken.None);

        Assert.DoesNotContain(
            "public",
            response.Headers.CacheControl?.ToString() ?? string.Empty,
            StringComparison.Ordinal);
    }


    /// <summary>
    /// Kontrollerar de tolkade direktiven, inte råsträngen. <c>HttpResponseHeaders</c>
    /// normaliserar ordningen vid parsning, så en strängjämförelse hade testat
    /// HttpClients formatering i stället för vår header.
    /// </summary>
    private static void AssertNoStore(HttpResponseMessage response)
    {
        var cacheControl = response.Headers.CacheControl;

        Assert.NotNull(cacheControl);
        Assert.True(cacheControl.NoStore, "no-store saknas");
        Assert.True(cacheControl.Private, "private saknas");
        Assert.False(cacheControl.Public, "svaret är märkt public");
        Assert.Null(cacheControl.SharedMaxAge);
    }

    [Fact]
    public void PrivateHeaderValue_ArDetViTror()
    {
        // Låser råsträngen på ett ställe, eftersom AssertNoStore läser tolkade flaggor.
        Assert.Equal("private, no-store", EdgeCache.PrivateHeaderValue);
    }

    // ---- Enhetsnära kontroller av delarna --------------------------------------------

    [Fact]
    public void ComputeETag_SammaInnehall_GerSammaTagg()
    {
        var a = EdgeCache.ComputeETag("hej"u8.ToArray());
        var b = EdgeCache.ComputeETag("hej"u8.ToArray());
        var c = EdgeCache.ComputeETag("då"u8.ToArray());

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.StartsWith("\"", a, StringComparison.Ordinal);
        Assert.EndsWith("\"", a, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"abc\"", true)]
    [InlineData("W/\"abc\"", true)]
    [InlineData("*", true)]
    [InlineData("\"annat\", \"abc\"", true)]
    [InlineData("\"annat\"", false)]
    [InlineData("", false)]
    public void MatchesClientVersion_TolkarIfNoneMatch(string header, bool expected)
    {
        var context = new DefaultHttpContext();

        if (!string.IsNullOrEmpty(header))
        {
            context.Request.Headers.IfNoneMatch = new StringValues(header);
        }

        Assert.Equal(expected, EdgeCache.MatchesClientVersion(context.Request, "\"abc\""));
    }

    [Fact]
    public void SecondsFor_TackerAllaProfiler()
    {
        var options = new EdgeCacheOptions();

        foreach (var profile in Enum.GetValues<EdgeCacheProfile>())
        {
            Assert.True(options.SecondsFor(profile) > 0, $"{profile} saknar livslängd");
        }
    }
}

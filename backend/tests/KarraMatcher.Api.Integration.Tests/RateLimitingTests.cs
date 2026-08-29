using System.Globalization;
using System.Net;

using KarraMatcher.Api.Diagnostics;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace KarraMatcher.Api.Integration.Tests;

public class RateLimitingTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    /// <summary>Sätter en låg gräns så att testet kan nå den utan hundratals anrop.</summary>
    private WebApplicationFactory<Program> WithLimit(int permitPerMinute) =>
        factory.WithWebHostBuilder(builder =>
            builder.UseSetting(RateLimiting.PermitKey, permitPerMinute.ToString(CultureInfo.InvariantCulture)));

    [Fact]
    public async Task NormalAnvandning_TraffarAldrigGransen()
    {
        // Gränsen är satt för att stoppa missbruk, inte en förälder som laddar om.
        using var app = WithLimit(50);
        using var client = app.CreateClient();

        for (var i = 0; i < 20; i++)
        {
            var response = await client.GetAsync("/health", CancellationToken.None);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task OverskridenGrans_Svarar429()
    {
        using var app = WithLimit(5);
        using var client = app.CreateClient();

        HttpResponseMessage? rejected = null;

        for (var i = 0; i < 10 && rejected is null; i++)
        {
            var response = await client.GetAsync("/health", CancellationToken.None);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                rejected = response;
            }
        }

        Assert.NotNull(rejected);
    }

    [Fact]
    public async Task OverskridenGrans_HarRetryAfter()
    {
        // Utan Retry-After vet klienten inte hur länge den ska vänta och hamrar vidare.
        using var app = WithLimit(3);
        using var client = app.CreateClient();

        HttpResponseMessage? rejected = null;

        for (var i = 0; i < 10 && rejected is null; i++)
        {
            var response = await client.GetAsync("/health", CancellationToken.None);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                rejected = response;
            }
        }

        Assert.NotNull(rejected);
        Assert.True(
            rejected.Headers.TryGetValues("Retry-After", out var values),
            "429-svaret saknade Retry-After");
        Assert.True(int.Parse(values!.Single(), CultureInfo.InvariantCulture) > 0);
    }

    [Fact]
    public async Task ForfalskadKlientadress_StoppasAndaAvSkyddsnatet()
    {
        // X-Forwarded-For gar att forfalska eftersom Render-URL:en ar publikt nabar.
        // En angripare som hittar pa en ny adress per anrop far en egen hink varje
        // gang — men den opartitionerade gransen gar inte att komma runt sa.
        using var app = WithLimit(2);   // skyddsnat = 2 * 20 = 40
        using var client = app.CreateClient();

        HttpResponseMessage? rejected = null;

        for (var i = 0; i < 80 && rejected is null; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
            request.Headers.TryAddWithoutValidation(
                "X-Forwarded-For", $"203.0.113.{i % 254 + 1}");

            var response = await client.SendAsync(request, CancellationToken.None);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                rejected = response;
            }
            else
            {
                response.Dispose();
            }
        }

        Assert.NotNull(rejected);
        rejected.Dispose();
    }
}

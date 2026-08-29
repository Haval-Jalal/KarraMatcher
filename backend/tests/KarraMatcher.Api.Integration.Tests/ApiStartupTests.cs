using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Startar hela API:t på riktigt via WebApplicationFactory. Faller DI-uppsättningen
/// i något lager syns det här, inte först vid deploy.
/// </summary>
public class ApiStartupTests : IClassFixture<KarraMatcherApiFactory>
{
    private readonly KarraMatcherApiFactory _factory;

    public ApiStartupTests(KarraMatcherApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Api_StartarOchSvararPaRoten()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

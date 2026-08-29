using System.Net;

using Microsoft.AspNetCore.Mvc.Testing;

namespace KarraMatcher.Api.Integration.Tests;

public class HealthCheckTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_ProcessenLever_Svarar200()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthReady_IngaTrasigaBeroenden_Svarar200()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthReady_TrasigtBeroende_Svarar503()
    {
        using var app = factory.WithFailingReadinessCheck();
        using var client = app.CreateClient();

        var response = await client.GetAsync("/health/ready", CancellationToken.None);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Health_TrasigtBeroende_SvararAndaOk()
    {
        // Liveness får inte falla på ett beroende. Annars startar Render om en
        // container som fungerar, bara för att databasen hackar.
        using var app = factory.WithFailingReadinessCheck();
        using var client = app.CreateClient();

        var response = await client.GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

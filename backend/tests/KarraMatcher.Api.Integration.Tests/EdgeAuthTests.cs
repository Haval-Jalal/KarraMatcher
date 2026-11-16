using System.Net;

using KarraMatcher.Api.Diagnostics;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Edge-skyddet (§KM.11, checklista 4.6, <c>#584</c>): backend avvisar trafik som gick förbi
/// Vercels proxy direkt mot Render-URL:en. Utan det kan en angripare förfalska en ny
/// <c>X-Forwarded-For</c> per anrop och få en egen rate-limit-hink varje gång (inkl. inloggning).
///
/// <para>
/// Rotendpointen <c>/</c> används som sond: den svarar <c>200</c> när grinden släppt igenom, så
/// skillnaden mot ett <c>403</c> säger rent om grinden stoppade eller inte.
/// </para>
/// </summary>
public sealed class EdgeAuthTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    [Fact]
    public async Task NarHemlighetSatt_UtanHeader_Avvisas()
    {
        using var client = factory.WithEdgeSecret().CreateClient();

        using var response = await client.GetAsync("/", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task NarHemlighetSatt_MedFelHeader_Avvisas()
    {
        using var client = factory.WithEdgeSecret().CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add(EdgeAuthentication.HeaderName, "fel-hemlighet-som-inte-stammer");

        using var response = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task NarHemlighetSatt_MedRattHeader_SlapperIgenom()
    {
        using var client = factory.WithEdgeSecret().CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add(EdgeAuthentication.HeaderName, TestApp.EdgeSecret);

        using var response = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    public async Task NarHemlighetSatt_HealthEndpointsNasUtanHeader(string path)
    {
        // Uppetidspingen (§KM.11) nar Render direkt och bar ingen edge-header. Exakt /health och
        // /health/ready maste darfor forbli oppna -- de rojer inga uppgifter.
        using var client = factory.WithEdgeSecret().CreateClient();

        using var response = await client.GetAsync(path, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task NarHemlighetSatt_UndergrenUnderHealth_Avvisas()
    {
        // Undantaget ar en exakt allowlist, inte hela /health-tradet: en vag under /health som
        // inte star pa listan gatas som standard (403), sa en framtida endpoint dar inte av
        // misstag slinker forbi grinden. Vagen finns inte an -- grinden stoppar den fore routingen.
        using var client = factory.WithEdgeSecret().CreateClient();

        using var response = await client.GetAsync("/health/admin", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UtanHemlighet_UtanHeader_SlapperIgenom()
    {
        // Forvalet: ingen hemlighet konfigurerad -> grinden ar inert och avvisar inget. Sa kan
        // koden rullas ut innan hemligheten finns pa bade Vercel och Render.
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

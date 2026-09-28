using System.Net;
using System.Text;

using KarraMatcher.Infrastructure.Geocoding;

using Microsoft.Extensions.Logging.Abstractions;

namespace KarraMatcher.Infrastructure.Tests;

/// <summary>
/// Photon-förslagen (`#307`): parsning, landsfilter, kodning och fail-soft.
///
/// <para>
/// Testerna stubbar HTTP-lagret i stället för att nå Photon på riktigt — ett as-you-type-test
/// mot en frivilligdriven tjänst vore både opålitligt och ohyfsat. Det som prövas är vår kod:
/// att bara svenska träffar tas med, att etiketten byggs läsbart, att det skrivna kodas in i
/// frågan, och att en trasig uppslagning ger en tom lista i stället för ett fel.
/// </para>
/// </summary>
public sealed class PhotonAddressSuggesterTests
{
    private const string TwoSwedishOneNorwegian = """
        {
          "features": [
            { "properties": { "name": "Klarebergsvallen", "street": "von Seths Gata",
              "housenumber": "3", "postcode": "425 32", "city": "Göteborg", "countrycode": "SE" } },
            { "properties": { "street": "Bortavägen", "housenumber": "5",
              "postcode": "442 30", "city": "Kungälv", "countrycode": "SE" } },
            { "properties": { "name": "Ullevaal Stadion", "city": "Oslo", "countrycode": "NO" } }
          ]
        }
        """;

    private const string EmptyFeatures = """{ "features": [] }""";

    private static PhotonAddressSuggester Suggester(HttpMessageHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://photon.komoot.io/") },
            NullLogger<PhotonAddressSuggester>.Instance);

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task SuggestAsync_TarBaraSvenskaTraffarOchByggerLasbaraEtiketter()
    {
        var suggester = Suggester(new StubHandler(_ => Json(TwoSwedishOneNorwegian)));

        var result = await suggester.SuggestAsync("von seths", CancellationToken.None);

        // Den norska träffen filtreras bort; namn föredras framför gata, orten läggs till.
        string[] expected = ["Klarebergsvallen, 425 32 Göteborg", "Bortavägen 5, 442 30 Kungälv"];
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task SuggestAsync_KortTerm_GerTomListaUtanAttAnropaPhoton()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("Skulle inte anropas."));

        var result = await Suggester(handler).SuggestAsync("vo", CancellationToken.None);

        Assert.Empty(result);
        Assert.False(handler.WasCalled);
    }

    [Fact]
    public async Task SuggestAsync_NarUppslagningenFallerar_GerTomLista()
    {
        var suggester = Suggester(new StubHandler(_ => throw new HttpRequestException("nere")));

        var result = await suggester.SuggestAsync("von seths gata", CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task SuggestAsync_KodarDetSkrivnaOchFragarPaSvenska()
    {
        var handler = new StubHandler(_ => Json(EmptyFeatures));

        await Suggester(handler).SuggestAsync("Kungälv & co", CancellationToken.None);

        // Den escaped formen, inte Uri.ToString() som avkodar för visning: poängen är att det
        // skrivna gick in kodat (& blir %26, inte en ny parameter) och inte konkatenerat rått.
        var query = handler.LastRequest!.RequestUri!.GetComponents(
            UriComponents.Query,
            UriFormat.UriEscaped);
        Assert.Contains("q=Kung%C3%A4lv%20%26%20co", query, StringComparison.Ordinal);
        Assert.Contains("lang=sv", query, StringComparison.Ordinal);
    }

    /// <summary>Fångar anropet och svarar med det testet bestämt — eller kastar för fail-soft.</summary>
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public bool WasCalled { get; private set; }

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            LastRequest = request;

            return Task.FromResult(respond(request));
        }
    }
}

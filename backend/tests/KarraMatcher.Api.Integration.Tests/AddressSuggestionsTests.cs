using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using KarraMatcher.Application.Abstractions.Geocoding;
using KarraMatcher.Application.Abstractions.Security;
using KarraMatcher.Application.Features.Auth;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Adress-förslag (`#307`): behörighet och genomströmning, prövat genom hela pipelinen.
///
/// <para>
/// Photon byts mot en fejk så testet varken når tredjeparten eller blir flakigt — det som prövas
/// är att endpointen kräver tränare/admin (§KM.3), att en gäst nekas, och att förslagen når ut
/// oförändrade. Den riktiga Photon-parsningen prövas som enhetstest i Infrastructure.
/// </para>
/// </summary>
public sealed class AddressSuggestionsTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private sealed class FakeSuggester(IReadOnlyList<string> suggestions) : IAddressSuggester
    {
        public Task<IReadOnlyList<string>> SuggestAsync(
            string term,
            CancellationToken cancellationToken) => Task.FromResult(suggestions);
    }

    private WebApplicationFactory<Program> HostWith(IReadOnlyList<string> suggestions) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAddressSuggester>();
                services.AddSingleton<IAddressSuggester>(new FakeSuggester(suggestions));
            }));

    private static string CoachToken(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();

        return issuer.Issue(Guid.NewGuid(), "tranare@example.com", new AccountRoles(false, [], ["gul"]))
            .Token;
    }

    [Fact]
    public async Task Forslag_UtanToken_Nekas()
    {
        using var host = HostWith(["Klarebergsvallen, Göteborg"]);
        using var client = host.CreateClient();

        var response = await client.GetAsync(
            "/api/v1/address-suggestions?q=klare",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Forslag_SomTranare_GerForslagenOforandrade()
    {
        string[] forslag = ["Klarebergsvallen, 425 32 Göteborg", "Bortavägen 5, 442 30 Kungälv"];
        using var host = HostWith(forslag);
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CoachToken(host.Services));

        var result = await client.GetFromJsonAsync<string[]>(
            "/api/v1/address-suggestions?q=klare",
            CancellationToken.None);

        Assert.Equal(forslag, result);
    }
}

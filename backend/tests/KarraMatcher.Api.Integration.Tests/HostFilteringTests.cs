using System.Net;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Host-header-filtrering (säkerhetschecklista 4.3/4.6, <c>#611</c>). I drift är <c>AllowedHosts</c>
/// pinnad till Render-hostarna, så en request med en främmande Host-header (cache-poisoning,
/// host-header-attack) avvisas med <c>400</c> i stället för att besvaras.
///
/// <para>
/// Fabriken sätter annars <c>AllowedHosts=*</c>; här pinnas en känd host per test för att pröva
/// att filtreringen verkligen är aktiv.
/// </para>
/// </summary>
public sealed class HostFilteringTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private const string AllowedHost = "karra.example";

    private HttpClient PinnedClient() =>
        factory
            .WithWebHostBuilder(builder => builder.UseSetting("AllowedHosts", AllowedHost))
            .CreateClient();

    [Fact]
    public async Task FrammandeHost_Avvisas()
    {
        using var client = PinnedClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Host = "evil.example";

        using var response = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TillatenHost_SlapperIgenom()
    {
        using var client = PinnedClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Host = AllowedHost;

        using var response = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

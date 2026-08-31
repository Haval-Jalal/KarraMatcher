using KarraMatcher.Application.Abstractions.Email;
using KarraMatcher.Infrastructure;
using KarraMatcher.Infrastructure.Email;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Infrastructure.Tests;

/// <summary>
/// Vilken mejlleverantör som väljs.
///
/// <para>
/// Det sämsta utfallet vore att tyst låta bli att skicka inloggningskoder i drift: allt
/// ser ut att fungera, ingen kommer in, och felet upptäcks först när en förälder hör av
/// sig en lördagsmorgon. Därför faller uppstarten i stället.
/// </para>
///
/// <para>
/// Utvecklingsavsändaren skriver koden i konsolen, vilket §KM.10 annars förbjuder. Att
/// den aldrig kan väljas utanför utveckling är alltså inte en detalj — det är hela skälet
/// till att den får finnas.
/// </para>
/// </summary>
public sealed class EmailSenderSelectionTests
{
    private static IConfiguration Configuration(string? apiKey)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Host=test;Database=test;Username=test;Password=test",
        };

        if (apiKey is not null)
        {
            values["Email:ApiKey"] = apiKey;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact]
    public void UtanNyckel_IDrift_FallerUppstarten()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddInfrastructure(Configuration(null), isDevelopment: false));

        Assert.Contains("ApiKey", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UtanNyckel_IUtveckling_SkriverMejletIKonsolen()
    {
        // Annars går det inte att logga in lokalt alls: koden lagras hashad och finns
        // bara i mejlet.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(Configuration(null), isDevelopment: true);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<DevelopmentEmailSender>(
            scope.ServiceProvider.GetRequiredService<IEmailSender>());
    }

    [Fact]
    public void MedNyckel_AnvandsLeverantoren()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(Configuration("re_testnyckel"), isDevelopment: true);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Även i utveckling: finns en nyckel ska riktiga mejl gå iväg, annars går det
        // inte att pröva den riktiga vägen innan lansering.
        Assert.IsType<ResendEmailSender>(
            scope.ServiceProvider.GetRequiredService<IEmailSender>());
    }
}

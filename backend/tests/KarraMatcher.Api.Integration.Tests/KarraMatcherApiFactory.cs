using KarraMatcher.Infrastructure.Persistence;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Startar API:t med en databas i minnet i stället för Postgres.
///
/// Testerna här verifierar HTTP-lagret — pipeline, felhantering, health checks.
/// Att den riktiga Npgsql-modellen fungerar mot Postgres bevisas i stället av
/// migrationens genererade SQL och av modelltesterna. Riktiga databastester
/// kräver Docker, vilket den här maskinen saknar.
/// </summary>
public sealed class KarraMatcherApiFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Databasnamnet måste vara stabilt för hela fabriken, men unikt mellan fabriker.
    ///
    /// <para>
    /// <c>AddDbContext</c> kör sin options-lambda varje gång optionsobjektet byggs, alltså
    /// en gång per scope. Ett <c>Guid.NewGuid()</c> inne i lambdan hade därför gett varje
    /// scope en <em>egen tom</em> databas — ett test som lägger in data i ett scope och
    /// läser i ett annat hade sett ingenting, utan att något såg trasigt ut.
    /// </para>
    /// </summary>
    private readonly string _databaseName = $"karra-test-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // AddInfrastructure vägrar starta utan anslutningssträng. Det är avsiktligt,
        // så testet får ge den en — värdet används aldrig eftersom providern byts ut.
        //
        // UseSetting och inte ConfigureAppConfiguration: Program.cs läser
        // konfigurationen redan när tjänsterna registreras, alltså innan
        // ConfigureAppConfiguration hinner köra.
        builder.UseSetting(
            "ConnectionStrings:Default",
            "Host=test;Database=test;Username=test;Password=test");

        // Testerna styr sin egen databaslivscykel. Utan det här skulle
        // appsettings.Development.json slå på migrationer, som in-memory-providern
        // inte stödjer — testerna körs i Development-miljön.
        // Inloggningens nyckel valideras vid start. Ett fast testvarde, aldrig ett
        // riktigt: det har far garna sta i klartext i ett publikt repo.
        builder.UseSetting("Auth:SigningKey", "testnyckel-som-bara-anvands-i-tester-0123456789");

        builder.UseSetting(DatabaseInitializer.MigrateKey, "false");
        builder.UseSetting(DatabaseInitializer.SeedKey, "false");

        builder.ConfigureServices(services =>
        {
            // EF tillater bara en provider per context. Npgsql-registreringen maste
            // bort helt - och den bestar av flera poster, inte bara DbContextOptions.
            var doomed = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<KarraMatcherDbContext>)
                    || d.ServiceType == typeof(DbContextOptions)
                    || d.ServiceType == typeof(KarraMatcherDbContext)
                    || (d.ServiceType.IsGenericType
                        && d.ServiceType.GetGenericArguments().Contains(typeof(KarraMatcherDbContext))))
                .ToList();

            foreach (var descriptor in doomed)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<KarraMatcherDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }
}

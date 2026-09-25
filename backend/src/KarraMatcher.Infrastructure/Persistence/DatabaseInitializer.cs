using KarraMatcher.Infrastructure.Persistence.Seed;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KarraMatcher.Infrastructure.Persistence;

/// <summary>
/// Kör migrationer och startdata vid uppstart — men bara när konfigurationen
/// uttryckligen ber om det.
///
/// Båda flaggorna är <c>false</c> som standard. Att ändra ett databasschema är inget
/// som ska ske av bara farten för att någon råkade starta appen mot fel databas.
/// Lokalt slås de på i appsettings.Development.json, i drift som miljövariabler.
/// </summary>
public static partial class DatabaseInitializer
{
    public const string MigrateKey = "Database:ApplyMigrationsOnStartup";
    public const string SeedKey = "Database:SeedOnStartup";
    public const string DemoLoginKey = "DemoSeed:Enabled";

    public static async Task InitializeDatabaseAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(DatabaseInitializer));

        if (Enabled(configuration, MigrateKey))
        {
            var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
            LogMigrating(logger);
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }

        if (Enabled(configuration, SeedKey))
        {
            var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
            var result = await seeder.SeedAsync(cancellationToken).ConfigureAwait(false);
            LogSeeded(logger, result.Teams, result.Venues, result.MatchesAdded);
        }

        // Skyddsräcke (#269): en fast demo-kod är en avsiktlig bakdörr. Säg det högt vid varje
        // start så den inte råkar bli kvar på i skarp drift.
        if (Enabled(configuration, DemoLoginKey))
        {
            LogDemoLoginActive(logger);
        }
    }

    /// <summary>Allt utom ett uttryckligt "true" betyder av.</summary>
    private static bool Enabled(IConfiguration configuration, string key) =>
        bool.TryParse(configuration[key], out var enabled) && enabled;

    [LoggerMessage(EventId = 2000, Level = LogLevel.Information,
        Message = "Applicerar databasmigrationer")]
    private static partial void LogMigrating(ILogger logger);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information,
        Message = "Startdata klar: {Teams} lag, {Venues} spelplatser, {MatchesAdded} nya matcher")]
    private static partial void LogSeeded(ILogger logger, int teams, int venues, int matchesAdded);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Warning,
        Message = "DEMO-INLOGGNING AR PA: demokontona loggar in med en fast kod. Stang av (DemoSeed:Enabled=false) fore lansering.")]
    private static partial void LogDemoLoginActive(ILogger logger);
}

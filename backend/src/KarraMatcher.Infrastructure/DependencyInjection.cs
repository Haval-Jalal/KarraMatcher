using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Infrastructure.Persistence;
using KarraMatcher.Infrastructure.Persistence.Repositories;
using KarraMatcher.Infrastructure.Persistence.Seed;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Infrastructure;

/// <summary>Registrerar infrastrukturlagrets tjänster.</summary>
public static class DependencyInjection
{
    public const string ConnectionStringName = "Default";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Anslutningssträngen kommer alltid från konfiguration — aldrig från kod.
        // Lokalt via user-secrets eller .env, i drift som miljövariabel i Render.
        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Anslutningssträngen '{ConnectionStringName}' saknas. Sätt "
                + $"ConnectionStrings__{ConnectionStringName} som miljövariabel, "
                + "eller kör 'dotnet user-secrets' lokalt. Se backend/.env.example.");
        }

        services.AddDbContext<KarraMatcherDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                // Neon stänger av beräkningen vid inaktivitet och startar den igen vid
                // nästa anslutning. Ett par försök gör den återstarten osynlig.
                npgsql.EnableRetryOnFailure(maxRetryCount: 3, TimeSpan.FromSeconds(5), null);
                npgsql.MigrationsAssembly(typeof(DependencyInjection).Assembly.FullName);
            }));

        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<ITeamRepository, TeamRepository>();

        // Databaskontrollen taggas "ready". Därmed faller /health/ready när databasen
        // är onåbar, medan /health fortsätter svara — se §KM.11 och issue #8.
        services.AddHealthChecks()
            .AddDbContextCheck<KarraMatcherDbContext>("database", tags: ["ready"]);

        return services;
    }
}
